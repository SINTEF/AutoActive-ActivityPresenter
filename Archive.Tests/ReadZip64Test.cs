using System;
using Xunit;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Archive.Plugin;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using System.Diagnostics;

namespace SINTEF.AutoActive.Archive.Tests
{
    public class ReadZip64Test
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


        [Fact]
        public async void ReadHakadalTest1Experiment()
        {
            var aazName = "C:\\Users\\steffend\\Documents\\repos\\autoactive_repos\\examples\\pilot_studie_hakadal\\pilot_hakadal_test_0_sync_2.aaz";

            var fr = new FileReader(aazName);
            using (var zipFile = new ZipFile(await fr.GetReadWriteStream()))
            {
                zipFile.UseZip64 = UseZip64.On;

                // Find all sessions in the archive
                foreach (ZipEntry entry in zipFile)
                {
                    if (entry.IsFile && entry.CompressionMethod == CompressionMethod.Stored )
                    {
                        using (var stream = await zipFile.OpenReadSeekStream(entry, fr))
                        {
                            if (entry.Name.EndsWith(".json"))
                            {
                                using (var streamReader = new StreamReader(stream))
                                {
                                    var line = streamReader.ReadLine();
                                    Debug.WriteLine(line);
                                }
                            }
                        }
                    }
                }

            }
        }
    }
}
