using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using Xunit;

namespace SINTEF.AutoActive.Archive.Tests
{
    public class ArchiveCreation
    {
        private class FileReader : IReadWriteSeekStreamFactory
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
                    _stream.Close();
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
            if (!Equals(a.DataPoints, b.DataPoints))
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

            return false;
        }

        private class SessionComparer : IEqualityComparer<ArchiveSession>
        {
            public bool Equals(ArchiveSession a, ArchiveSession b)
            {
                if (!DataStructureEquals(a, b)) return false;
                
                if (a.Id != b.Id) return false;
                if (a.Type != b.Type) return false;
                if (a.Name != b.Name) return false;
                if (a.Created != b.Created) return false;

                return true;
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

        [Fact]
        public void CreateEmptySingleArchive()
        {
            var tmpName = Path.GetTempFileName();
            var archive = Archive.Create(tmpName);

            var session = ArchiveSession.Create(archive, "testName");
            archive.AddSession(session);
            archive.WriteFile();
            archive.Close();

            var openTask = Archive.Open(new FileReader(tmpName));
            openTask.Wait();
            var outArchive = openTask.Result;
            AssertArchivesEqual(archive, outArchive);
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

            var openTask = Archive.Open(new FileReader(tmpName));
            openTask.Wait();
            AssertArchivesEqual(archive, openTask.Result);
        }

        [Fact]
        public void CreateSingleDataArchive()
        {
            var tmpName = Path.GetTempFileName();
            var archive = Archive.Create(tmpName);

            var session = ArchiveSession.Create(archive, "testName");
            var folder = ArchiveFolder.Create(archive);
            session.AddChild(folder);
            archive.AddSession(session);

            archive.WriteFile();
            archive.Close();

            var openTask = Archive.Open(new FileReader(tmpName));
            openTask.Wait();
            AssertArchivesEqual(archive, openTask.Result);
        }


        private class Storeable : IStaticDataSource
        {
            private readonly Stream _stream;

            public Storeable(Stream stream)
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
            zip.Add(new Storeable(ms), "testFile.bin", CompressionMethod.Stored);
            zip.CommitUpdate();

            zip.Close();

            var outZip = new ZipFile(tmpName);
            Assert.Equal(0, outZip.FindEntry("testFile.bin", false));

            var ze = outZip.GetEntry("testFile.bin");
            var inStream = outZip.GetInputStream(ze);
            var outBytes = new byte[1024];
            inStream.Read(outBytes, 0, (int)inStream.Length);
            Assert.Equal(tmpBytes, outBytes);
        }
    }
}
