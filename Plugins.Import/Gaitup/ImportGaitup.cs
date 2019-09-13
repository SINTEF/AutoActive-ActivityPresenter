using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GaitupParser;
using Newtonsoft.Json.Linq;
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
            var name = readerFactory.Name;
            var fileName = name + readerFactory.Extension;
            var parser = new GaitupParser.GaitupParser(stream, name, fileName);
            GaitupSessionImporter sessionImporter = null;

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
                    sessionImporter = new GaitupSessionImporter(SessionData);
                    var synchronizer = new GaitupSynchronizer(SessionData);
                    synchronizer.Synchronize();
                    synchronizer.CropSets();

                }
                _isFirst = false;
            }

            //return sessionImporter.GetImporter(fileName);
            return sessionImporter;
        }
    }

    public class GaitupSessionImporter : BaseDataProvider
    {
        public GaitupSessionImporter(List<GaitupData> sessionData)
        {
            Name = "GaitupSessionImport";
            AddChild(new GaitupTop(sessionData));
        }

        protected override void DoParseFile(Stream s)
        {
            // Files already parsed and synced ... Nothing more to do
        }

    }

    public class GaitupTop : BaseDataStructure, ISaveable
    {
        public bool IsSaved { get; }
        private List<GaitupData> _sessionData = null;
        //private Dictionary<string, GaitupImporter> _importers = new Dictionary<string, GaitupImporter>();
        public GaitupTop(List<GaitupData> sessionData)
        {
            Name = "Gaitup";
            _sessionData = sessionData;
            IsSaved = false;

            foreach (var data in _sessionData)
            {
                var importer = new GaitupFolder(data);
                //      _importers.Add(data.Config.FileName, importer);
                AddChild(importer);
            }
        }


        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {

            // Make folder object
            var metaFolder = new JObject { ["type"] = "no.sintef.gaitup" };
            metaFolder["version"] = 1;
            var userFolder = new JObject { };

            // Place objects into root
            root["meta"] = metaFolder;
            root["user"] = userFolder;

            return true;

        }
    }

    public class GaitupFolder : BaseDataStructure, ISaveable
    {
        public bool IsSaved { get; }
        private GaitupData _data;
        public GaitupFolder(GaitupData data)
        {
            _data = data;
            Name = "sensor" + data.Config.Name;
            IsSaved = false;
            AddChild(new AccTable(data.Accelerometer));
        }

        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            var sensorConf = _data.Config.Sensor;
            var garminInfo = new JObject { };
            garminInfo["deviceId"] = sensorConf.DeviceId;
            garminInfo["deviceType"] = sensorConf.DeviceType;
            garminInfo["bodyLocation"] = sensorConf.BodyLocation;
            garminInfo["firmawareVersion"] = "" + sensorConf.Version + "." + sensorConf.MajorVersion + "." + sensorConf.MinorVersion;

            // Make folder object
            var metaFolder = new JObject { ["type"] = "no.sintef.folder" };
            metaFolder["version"] = 1;
            var userFolder = new JObject { };
            userFolder["info"] = garminInfo;


            // Place objects into root
            root["meta"] = metaFolder;
            root["user"] = userFolder;

            return true;

        }

    }

    public class AccTable : ImportTableBase, ISaveable
    {
        public bool IsSaved { get; }
        public IReadOnlyList<(long, double, double, double)> _data;
        internal AccTable(IReadOnlyList<(long, double, double, double)> data)
        {
            _data = data;
            Name = "accel";
            IsSaved = false;

            bool isWorldSynchronized = false;
            string columnName = "time";
            string uri = Name + "/" + columnName;

            _timeIndex = new TableTimeIndex(columnName, GenerateLoader<long>(columnName), isWorldSynchronized, uri);

            columnName = "data_accel1";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<double>(columnName), _timeIndex, uri);

            columnName = "data_accel2";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<double>(columnName), _timeIndex, uri);

            columnName = "data_accel3";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<double>(columnName), _timeIndex, uri);

        }

        public override Dictionary<string, Array> ReadData()
        {
            var dataDict = new Dictionary<string, Array>();
            dataDict.Add("time", _data.Select(el => el.Item1).ToArray());
            dataDict.Add("data_accel1", _data.Select(el => el.Item2).ToArray());
            dataDict.Add("data_accel2", _data.Select(el => el.Item3).ToArray());
            dataDict.Add("data_accel3", _data.Select(el => el.Item4).ToArray());
            return dataDict;
        }


        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {

            string fileId;

            // TODO: give a better name?
            fileId = "/Import" + "/" + Name + "." + Guid.NewGuid();

            // Make table object
            var metaTable = new JObject { ["type"] = "no.sintef.table" };
            metaTable["attachments"] = new JArray(new object[] { fileId });
            metaTable["units"] = new JArray(new object[] { });
            metaTable["is_world_clock"] = _timeIndex.IsSynchronizedToWorldClock;
            metaTable["version"] = 1;

            var userTable = new JObject { };

            // Place objects into root
            root["meta"] = metaTable;
            root["user"] = userTable;

            bool result = await WriteTable(fileId, writer);
            return result;

        }

    }
}

