using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GaitupParser;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.Import.Gaitup
{
    [ImportPlugin(".bin")]
    public class ImportGaitupPlugin : IBatchImportPlugin
    {
        public List<GaitupData> SessionData = new List<GaitupData>();

        private Barrier _transactionBarrier;
        private readonly Mutex _transactionMutex = new Mutex();
        private bool _isFirst;
        

        public void StartTransaction(int numFiles)
        {
            SessionData.Clear();
            _isFirst = true;
            _transactionBarrier = new Barrier(numFiles);
        }

        public void EndTransaction()
        {
        }

        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory)
        {
            var stream = await readerFactory.GetReadStream();

            var parser = new GaitupParser.GaitupParser(stream);
            parser.ParseFile();

            stream.Close();

            var data = parser.GetData();

            SessionData.Add(data);

            if (!_transactionBarrier.SignalAndWait(1000))
            {
                throw new TimeoutException("Could not find all gaitup files in session.");
            }

            lock (_transactionMutex)
            {
                if (_isFirst)
                {
                    var synchronizer = new GaitupSynchronizer(SessionData);
                    synchronizer.Synchronize();
                    synchronizer.CropSets();
                    
                }
                _isFirst = false;
            }

            var importer = new GaitupImporter(data);

            importer.RegisterGaitup();

            return importer;
        }
    }

    public class GaitupImporter : BaseDataProvider
    {
        private GaitupData _data;
        public GaitupImporter(GaitupData data)
        {
            _data = data;
        }

        protected override void DoParseFile(Stream stream)
        {
        }

        public void RegisterGaitup()
        {
            Name = "Gaitup";

            var nEl = _data.Accelerometer.Count;
            var time = _data.Accelerometer.Select(el => el.Item1).ToArray();
            var accX = _data.Accelerometer.Select(el => el.Item2).ToArray();
            var accY = _data.Accelerometer.Select(el => el.Item3).ToArray();
            var accZ = _data.Accelerometer.Select(el => el.Item4).ToArray();
            
        }
    }
}
