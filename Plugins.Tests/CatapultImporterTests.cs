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


        [Fact]
        public async void CheckMemFree()
        {
            double mid1MemM = 0;
            double mid2MemM = 0;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var startMem = GC.GetTotalMemory(true);
            var startMemM = startMem / 1024.0 / 1024.0;
            {
                var csvName = "C:\\Users\\steffend\\Documents\\repos\\autoactive_repos\\examples\\import_catapult\\73220 2 7684 201811071247.csv";

                var fr = new FileReader(csvName);

                var importer = new CatapultImportPlugin();
                var provider = await importer.Import(fr, null);
                var childEnum = provider.Children.GetEnumerator();
                childEnum.MoveNext();
                var table = childEnum.Current;
                var dataEnum = table.DataPoints.GetEnumerator();
                dataEnum.MoveNext();
                var data = dataEnum.Current;
                var viewer = await data.CreateViewer();

                mid1MemM = GC.GetTotalMemory(true) / 1024.0 / 1024.0;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                mid2MemM = GC.GetTotalMemory(true) / 1024.0 / 1024.0;

                AllocLogger.PrintRegs();
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var endMem = GC.GetTotalMemory(true);
            var endMemM = endMem / 1024.0 / 1024.0;
            AllocLogger.PrintRegs();
        }

    }
}
