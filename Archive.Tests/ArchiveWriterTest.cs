using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using Parquet;
using Parquet.Data;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Table;
using Xunit;
using CompressionMethod = ICSharpCode.SharpZipLib.Zip.CompressionMethod;

namespace SINTEF.AutoActive.Archive.Tests
{
    public class ArchiveWriterTest
    {
        private class FileReader : IReadWriteSeekStreamFactory, IDisposable
        {
            private FileStream _stream;
            internal FileReader(string name)
            {
                Name = name;
            }
            public string Name { get; }
            public string Extension { get; }
            public string Mime { get; }

            public Task<Stream> GetReadStream()
            {
                if (_stream == null)
                {
                    _stream = File.Open(Name, FileMode.Open, FileAccess.Read);
                }
                var task = new Task<Stream>(() => _stream);
                task.Start();
                return task;
            }

            public Task<Stream> GetReadWriteStream()
            {
                if (_stream != null && !_stream.CanWrite)
                {
                    _stream.Dispose();
                    _stream = null;
                }

                if (_stream == null)
                {
                    _stream = File.Open(Name, FileMode.Open, FileAccess.ReadWrite);
                }

                var task = new Task<Stream>(() => _stream);
                task.Start();
                return task;
            }

            public void Close()
            {
                _stream?.Dispose();
                _stream = null;
            }

            public void Dispose()
            {
                _stream?.Dispose();
            }
        }

        public static bool DataPointComparator(IDataPoint a, IDataPoint b)
        {
            if (a.DataType != b.DataType)
                return false;
            if (a.Name != b.Name)
                return false;
            if (a.Time != b.Time)
                return false;

            return true;
        }

        public static bool DataStructureEquals(IDataStructure a, IDataStructure b)
        {
            if (a.GetType() != b.GetType())
                return false;
            if (a.Name != b.Name)
                return false;
            var aCount = a.DataPoints.Count();
            if (aCount != b.DataPoints.Count())
                return false;

            var enA = a.DataPoints.GetEnumerator();
            var enB = b.DataPoints.GetEnumerator();
            try
            {
                for (var i = 0; i < aCount; i++)
                {
                    if (DataPointComparator(enA.Current, enB.Current))
                        return false;
                }
            }
            finally
            {
                enA.Dispose();
                enB.Dispose();
            }

            return true;
        }

        private class SessionComparer : IEqualityComparer<ArchiveSession>
        {
            public bool Equals(ArchiveSession a, ArchiveSession b)
            {
                if (a.Id != b.Id) return false;
                if (a.Type != b.Type) return false;
                if (a.Name != b.Name) return false;
                if (a.Created != b.Created) return false;

                return DataStructureEquals(a, b);
            }

            public int GetHashCode(ArchiveSession obj)
            {
                return obj.Id.GetHashCode();
            }
        }

        private static void AssertArchivesEqual(Archive archiveExpected, Archive archiveActual)
        {
            var nSessions = archiveExpected.Sessions.Count;
            Assert.Equal(nSessions, archiveActual.Sessions.Count);
            var sessionEnA = archiveExpected.Sessions.GetEnumerator();
            var sessionEnB = archiveActual.Sessions.GetEnumerator();

            try
            {
                for (var i = 0; i < nSessions; i++)
                {
                    sessionEnA.MoveNext();
                    sessionEnB.MoveNext();
                    Assert.Equal(sessionEnA.Current, sessionEnB.Current, new SessionComparer());
                }
            }
            finally
            {
                sessionEnA.Dispose();
                sessionEnB.Dispose();
            }
        }

        public class PluginTestInitializer: IPluginInitializer
        {
            IEnumerable<Type> IPluginInitializer.Plugins => new[] {
                // Archive plugins
                typeof(ArchiveFolderPlugin),
                typeof(ArchiveSessionPlugin),
                typeof(ArchiveTablePlugin)
            };
        }

        [Fact]
        public void OpenSingleArchive()
        {
            using (var fr = new FileReader(@"testdata\empty.aaz"))
            {
                var openTask = Archive.Open(fr);
                openTask.Wait();
                var archive = openTask.Result;
                Assert.Equal(1, archive.Sessions.Count);
                archive.Close();
            }
        }

        [Fact]
        public void ReSaveSingleArchive()
        {
            var tmpName = Path.GetTempFileName();
            using (var testdataReader = new FileReader(@"testdata\empty.aaz"))
            {
                var openTask = Archive.Open(testdataReader);
                openTask.Wait();
                var archive = openTask.Result;
                Assert.Equal(1, archive.Sessions.Count);
                
                archive.WriteFile(tmpName);
                archive.Close();

                using (var fr = new FileReader(tmpName))
                {
                    openTask = Archive.Open(fr);
                    openTask.Wait();
                    var readArchive = openTask.Result;
                    AssertArchivesEqual(archive, readArchive);

                    readArchive.Close();
                }
            }
            File.Delete(tmpName);
        }


        [Fact]
        public void CreateEmptySingleArchive()
        {
            var tmpName = Path.GetTempFileName();
            var archive = Archive.Create(tmpName);

            var session = ArchiveSession.Create(archive, "testName");
            archive.AddSession(session);
            archive.WriteFile();
            archive.Close();

            using (var fr = new FileReader(tmpName))
            {
                var openTask = Archive.Open(fr);
                openTask.Wait();
                var outArchive = openTask.Result;
                AssertArchivesEqual(archive, outArchive);
            }

            File.Delete(tmpName);
        }

        [Fact]
        public void CreateEmptyDoubleArchive()
        {
            var tmpName = Path.GetTempFileName();
            var archive = Archive.Create(tmpName);

            var session = ArchiveSession.Create(archive, "testName");
            var session2 = ArchiveSession.Create(archive, "testName2");

            archive.AddSession(session);
            archive.AddSession(session2);

            archive.WriteFile();
            archive.Close();

            using (var fr = new FileReader(tmpName))
            {
                var openTask = Archive.Open(fr);
                openTask.Wait();
                AssertArchivesEqual(archive, openTask.Result);

                archive.Close();
            }

            File.Delete(tmpName);
        }

        [Fact]
        public void SaveSingleDataArchive()
        {
            var tmpName = Path.GetTempFileName();

            var timeData = new[] {1L, 2L, 3L};
            var timeColumn = new DataColumn(
                new DataField<long>("time"),
                timeData);

            var numbersData = new[] {42d, 1337d, 6.022e23};
            var numbersColumn = new DataColumn(
                new DataField<double>("cool_numbers"),
                numbersData);

            var schema = new Schema(timeColumn.Field, numbersColumn.Field);

            var json = new JObject {["meta"] = new JObject(), ["user"] = new JObject()};


            using (var ms = new MemoryStream())
            {
                using (var parquetWriter = new ParquetWriter(schema, ms))
                using (var groupWriter = parquetWriter.CreateRowGroup())
                {
                    groupWriter.WriteColumn(timeColumn);
                    groupWriter.WriteColumn(numbersColumn);
                }

                ms.Position = 0;

                using (var parquetReader = new ParquetReader(ms))
                {
                    var tableInformation = new ArchiveTableInformation()
                    {
                        Columns = new List<DataField>(parquetReader.Schema.GetDataFields()),
                        Time = timeColumn.Field
                    };
                    var table = new ArchiveTable(json, parquetReader, tableInformation, "testData");
                    
                    var archive = Archive.Create(tmpName);

                    var session = ArchiveSession.Create(archive, "testName");
                    var folder = ArchiveFolder.Create(archive, "testFolder");

                    folder.AddChild(table);
                    session.AddChild(folder);
                    archive.AddSession(session);

                    archive.WriteFile();
                    archive.Close();

                    using (var fr = new FileReader(tmpName))
                    {
                        var openTask = Archive.Open(fr);
                        openTask.Wait();
                        var newArchive = openTask.Result;
                        AssertArchivesEqual(archive, newArchive);

                        Assert.Equal("testName", session.Name);
                        Assert.Single(newArchive.Sessions.First().Children);
                        var readFolder = newArchive.Sessions.First().Children.First();
                        Assert.Equal("testFolder", readFolder.Name);
                        Assert.Single(readFolder.Children);

                        var child = readFolder.Children.First();
                        Assert.Single(child.DataPoints);

                        Assert.IsAssignableFrom<ArchiveTable>(child);
                        var tableChild = (ArchiveTable) child;
                        
                        var dataPoint = tableChild.DataPoints.First();
                        var context = new TimeSynchronizedContext();
                        context.AvailableTimeRangeChanged +=
                            (sender, from, to) => context.SetSelectedTimeRange(from, to);
                        
                        var viewer = context.GetDataViewerFor(dataPoint);
                        viewer.Wait();
                        var dataViewer = viewer.Result;

                        Assert.IsAssignableFrom<ITimeSeriesViewer>(dataViewer);
                        var timeViewer = (ITimeSeriesViewer) dataViewer;

                        var data = timeViewer.GetCurrentDoubles();
                        Assert.Equal("cool_numbers", dataViewer.DataPoint.Name);

                        Assert.Equal(timeData, data.X.ToArray());
                        Assert.Equal(numbersData, data.Y.ToArray());

                        openTask.Result.Close();
                    }
                }
            }

            //Debug.WriteLine(tmpName);
            File.Delete(tmpName);
        }

        private class Storable : IStaticDataSource
        {
            private readonly Stream _stream;

            public Storable(Stream stream)
            {
                _stream = stream;
            }
            public Stream GetSource()
            {
                return _stream;
            }
        }

        [Fact]
        public void SimpleZipFile()
        {
            var tmpName = Path.GetTempFileName();
            var zip = ZipFile.Create(tmpName);
            var ms = new MemoryStream();
            var rand = new Random();

            var tmpBytes = new byte[1024];
            rand.NextBytes(tmpBytes);
            ms.Write(tmpBytes);
            ms.Position = 0;

            zip.BeginUpdate();
            zip.Add(new Storable(ms), "testFile.bin", CompressionMethod.Stored);
            zip.CommitUpdate();

            zip.Close();

            var outZip = new ZipFile(tmpName);
            Assert.Equal(0, outZip.FindEntry("testFile.bin", false));

            var ze = outZip.GetEntry("testFile.bin");
            var inStream = outZip.GetInputStream(ze);
            var outBytes = new byte[1024];
            inStream.Read(outBytes, 0, (int)inStream.Length);
            Assert.Equal(tmpBytes, outBytes);

            outZip.Close();

            File.Delete(tmpName);
        }
    }
}
