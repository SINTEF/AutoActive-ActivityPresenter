using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.AllocCheck;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins.Import.Csv;
using SINTEF.AutoActive.UI.Helpers;
using Xunit;

namespace Plugins.Tests
{
    public class CatapultImporter
    {
        [Fact]
        public void TestParseTimestamp()
        {
            Assert.Equal(0L, CatapultParser.ConvHmssToEpochUs("0:00.00"));
            Assert.Equal(TimeFormatter.TimeFromTimeSpan(TimeSpan.FromMilliseconds(2290)), CatapultParser.ConvHmssToEpochUs("0:02.29"));
            Assert.Equal(TimeFormatter.TimeFromTimeSpan(new TimeSpan(0,0,51,50,0)), CatapultParser.ConvHmssToEpochUs("51:50.00"));
            Assert.Equal(TimeFormatter.TimeFromTimeSpan(new TimeSpan(0, 1, 9, 11, 0)), CatapultParser.ConvHmssToEpochUs("1:09:11.00"));
        }

        private class FileReader : IReadWriteSeekStreamFactory
        {
            protected List<Stream> Streams = new List<Stream>();
            internal FileReader(string name)
            {
                Name = name;
            }
            public string Name { get; }
            public string Extension { get; }
            public string Mime { get; }

            public Task<Stream> GetReadStream()
            {
                Stream locStream = null;
                Stream locBuffStream = null;
                try
                {
                    locStream = File.Open(Name, FileMode.Open, FileAccess.Read);
                    locBuffStream = new BufferedStream(locStream);
                    Streams.Add(locBuffStream);
                    Streams.Add(locStream);
                }
                catch (Exception)
                {
                    locBuffStream?.Close();
                    locStream?.Close();
                    throw;
                }

                var task = new Task<Stream>(() => locBuffStream);
                task.Start();
                return task;
            }

            public Task<Stream> GetReadWriteStream()
            {
                Stream locStream = null;
                Stream locBuffStream = null;
                try
                {
                    locStream = File.Open(Name, FileMode.Open, FileAccess.ReadWrite);
                    locBuffStream = new BufferedStream(locStream);
                    Streams.Add(locBuffStream);
                    Streams.Add(locStream);
                }
                catch (Exception)
                {
                    locBuffStream?.Close();
                    locStream?.Close();
                    throw;
                }

                var task = new Task<Stream>(() => locBuffStream);
                task.Start();
                return task;
            }

            public void Close()
            {
                foreach (var stream in Streams)
                {
                    stream.Close();
                }
            }

        }


        private int CheckMemFreeStuff()
        {
            var csvName = "C:\\Users\\steffend\\Documents\\repos\\autoactive_repos\\examples\\import_catapult\\73220 2 7684 201811071247.csv";
            var at = new AllocTrack(csvName, "csvName");

            var fr = new FileReader(csvName);

            var importer = new CatapultImportPlugin();
            var importTask = importer.Import(fr, null);
            importTask.Wait();
            var provider = importTask.Result;
            var childEnum = provider.Children.GetEnumerator();
            childEnum.MoveNext();
            var table = childEnum.Current;
            var dataEnum = table.DataPoints.GetEnumerator();
            dataEnum.MoveNext();
            var data = dataEnum.Current;

            //var viewerTask = data.CreateViewer();
            //viewerTask.Wait();
            //var viewer = viewerTask.Result;

            provider.Close();

            AllocLogger.PrintRegs();
            Assert.True(AllocLogger.GetTotalRegs() > 0, "No allocations registered");

            return AllocLogger.GetTotalRegs();
        }

        [Fact]
        public void CheckMemFree()
        {
            AllocLogger.ResetAll();
            var startMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;

            int numAlloc = CheckMemFreeStuff();

            var endMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;
            AllocLogger.PrintRegs();
            Assert.True(AllocLogger.GetTotalRegs() == 0, $"Not all allocations removed {AllocLogger.GetTotalRegs()} remains");

        }

        private void TestAllocLoggerStuff()
        {
            var csvName = "C:\\Users\\steffend\\Documents\\repos\\autoactive_repos\\examples\\import_catapult\\73220 2 7684 201811071247.csv";
            var at = new AllocTrack(csvName, "csvName");
            var buff = new long[1000000];
            var at2 = new AllocTrack(buff);

            AllocLogger.PrintRegs();
            Assert.True(AllocLogger.GetTotalRegs() > 0, "No allocations registered");
        }


        [Fact]
        public async void TestAllocLogger()
        {
            AllocLogger.ResetAll();
            var startMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;

            TestAllocLoggerStuff();

            var endMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;
            AllocLogger.PrintRegs();
            Assert.True(AllocLogger.GetTotalRegs() == 0, "Not all allocations removed");
        }
    }
}
