using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.AllocCheck;
using SINTEF.AutoActive.Databus.Interfaces;
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


        private void LoadAll(IDataStructure structure)
        {
            foreach (var child in structure.Children)
                LoadAll(child);

            foreach( var datapoint in structure.DataPoints)
            {
                var viewerTask = datapoint.CreateViewer();
                viewerTask.Wait();
                var viewer = viewerTask.Result;
            }
        }

        private int CheckMemFreeStuff(bool doLoad)
        {
            var csvName = "C:\\Users\\steffend\\Documents\\repos\\autoactive_repos\\examples\\import_catapult\\73220 2 7684 201811071247.csv";
            var at = new AllocTrack(csvName, "csvName");

            var fr = new TestFileReader(csvName);

            var importer = new CatapultImportPlugin();
            var importTask = importer.Import(fr, null);
            importTask.Wait();
            var provider = importTask.Result;

            if(doLoad)
                LoadAll(provider);

            var totRegs = AllocLogger.GetTotalRegs();
            AllocLogger.PrintRegs();
            Assert.True(totRegs > 0, "No allocations registered");

            provider.Close();

            return totRegs;
        }

        [Fact]
        public void CheckMemFreeNoLoad()
        {
            AllocLogger.ResetAll();
            var startMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;

            int numAlloc = CheckMemFreeStuff(false);

            var endMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;
            AllocLogger.PrintRegs();
            Assert.True(AllocLogger.GetTotalRegs() == 0, $"Not all allocations removed {AllocLogger.GetTotalRegs()} remains");

        }

        [Fact]
        public void CheckMemFreeWithLoad()
        {
            AllocLogger.ResetAll();
            var startMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;

            int numAlloc = CheckMemFreeStuff(true);

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
        public async void TestAllocLoggerFree()
        {
            AllocLogger.ResetAll();
            var startMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;

            TestAllocLoggerStuff();

            var endMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;
            AllocLogger.PrintRegs();
            Assert.True(AllocLogger.GetTotalRegs() == 0, "Not all allocations removed");
        }

        [Fact]
        public async void TestAllocLoggerHold()
        {
            AllocLogger.ResetAll();
            var startMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;

            var csvName = "C:\\Users\\steffend\\Documents\\repos\\autoactive_repos\\examples\\import_catapult\\73220 2 7684 201811071247.csv";
            var at = new AllocTrack(csvName, "csvName");

            var endMemM = AllocLogger.GetTotalMemory() / 1024.0 / 1024.0;
            AllocLogger.PrintRegs();
            Assert.True(AllocLogger.GetTotalRegs() > 0, "No allocations registered");
        }
    }
}
