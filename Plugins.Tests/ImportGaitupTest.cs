using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.AllocCheck;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins.Import.Gaitup;
using SINTEF.AutoActive.UI.Helpers;
using Xunit;

namespace Plugins.Tests
{
    public class ImportGaitupTest
    {
        private void LoadAll(IDataStructure structure)
        {
            foreach (var child in structure.Children)
                LoadAll(child);

            foreach (var datapoint in structure.DataPoints)
            {
                var viewerTask = datapoint.CreateViewer();
                viewerTask.Wait();
                var viewer = viewerTask.Result;
            }
        }

        private int CheckMemFreeStuff(bool doLoad)
        {
            var dataFolder = "C:\\Users\\steffend\\SINTEF\\AutoActive Internt - Dokumenter\\Data\\LabtestSentifNov-Des2018\\2. Data\\Sub2\\gaitup_imu\\";
            var at = new AllocTrack(dataFolder, "dataFolder");

            var fileList = new List<IReadSeekStreamFactory>();
            fileList.Add(new TestFileReader(dataFolder + "0ST211.BIN"));
            fileList.Add(new TestFileReader(dataFolder + "0LA210.BIN"));
            fileList.Add(new TestFileReader(dataFolder + "0LF100.BIN"));
            fileList.Add(new TestFileReader(dataFolder + "0RA212.BIN"));
            fileList.Add(new TestFileReader(dataFolder + "0RF214.BIN"));
            fileList.Add(new TestFileReader(dataFolder + "0SA010.BIN"));

            var importer = new ImportGaitupPlugin();

            IDataProvider provider = null;

            importer.StartTransaction(fileList);
            foreach (var file in fileList)
            {
                var parameters = new Dictionary<string, object>();
                parameters["Name"] = "Imported File";

                var importTask = importer.Import(file, parameters);
                importTask.Wait();

                var res = importTask.Result;
                if (res != null)
                    provider = res;

            }
            importer.EndTransaction();

            if (doLoad)
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

    }
}
