using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            lock (_transactionMutex)
            {
                SessionData.Add(data);
            }

            if (!_transactionBarrier.SignalAndWait(5000))
            {
                throw new TimeoutException("Could not find all gaitup files in session.");
            }

            lock (_transactionMutex)
            {
                if (_isFirst)
                {
                    Debug.WriteLine($"Start GaitUpSync {name}: {SessionData.Count}");
                    var synchronizer = new GaitupSynchronizer(SessionData);
                    synchronizer.Synchronize();
                    synchronizer.CropSets();
                    sessionImporter = new GaitupSessionImporter(SessionData);

                }
                _isFirst = false;
            }

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
        public GaitupTop(List<GaitupData> sessionData)
        {
            Name = "Gaitup";
            _sessionData = sessionData;
            IsSaved = false;

            foreach (var data in _sessionData)
            {
                var importer = new GaitupFolder(data);
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
            if(data.Accelerometer.Count > 0) AddChild(new AccTable(data.Accelerometer, data.Config.Frequency));
            if (data.Gyro.Count > 0) AddChild(new GyrTable(data.Gyro, data.Config.Frequency));
            if (data.Barometer.Count > 0) AddChild(new BaroTable(data.Barometer, data.Config.Frequency));
            if (data.Ble.Count > 0) AddChild(new BleTable(data.Ble, data.Config.Frequency));
            if (data.Radio.Count > 0) AddChild(new RadioTable(data.Radio, data.Config.Frequency));
        }

        public JObject MakeDate(DateTime date)
        {
            var jdate = new JObject {
                ["Year"] = date.Year,
                ["Month"] = date.Month,
                ["Day"] = date.Day,
                ["Hour"] = date.Hour,
                ["Minute"] = date.Minute,
                ["Seconds"] = date.Second };

            return jdate;
        }

        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            var sensorConf = _data.Config.Sensor;
            var conf = _data.Config;
            var accConf = _data.Config.Accelerometer;
            var gyroConf = _data.Config.Gyro;
            var baroConf = _data.Config.Barometer;
            var gaitupInfo = new JObject { };
            gaitupInfo["deviceId"] = sensorConf.DeviceId;
            gaitupInfo["deviceType"] = sensorConf.DeviceType;
            gaitupInfo["bodyLocation"] = sensorConf.BodyLocation;
            gaitupInfo["firmawareVersion"] = "" + sensorConf.Version + "." + sensorConf.MajorVersion + "." + sensorConf.MinorVersion;
            gaitupInfo["baseFrequency"] = conf.Frequency;
            gaitupInfo["measureID"] = conf.MeasureId;
            gaitupInfo["filename"] = conf.FileName;
            gaitupInfo["startDate"] = MakeDate(conf.StartDate);
            gaitupInfo["stopDate"] = MakeDate(conf.StopDate);
            gaitupInfo["sensor"] = new JObject { };
            gaitupInfo["sensor"]["accel"] = new JObject { };
            var accelInfo = gaitupInfo["sensor"]["accel"];
            accelInfo["scale"] = accConf.Scale;
            accelInfo["sensorId"] = accConf.Id;
            accelInfo["FS"] = accConf.SamplingFrequency;
            accelInfo["dataPayload"] = accConf.PayloadLength;
            accelInfo["nbSamples"] = accConf.NumberOfSamples;
            gaitupInfo["sensor"]["gyro"] = new JObject { };
            var gyroInfo = gaitupInfo["sensor"]["gyro"];
            gyroInfo["scale"] = gyroConf.Scale;
            gyroInfo["sensorId"] = gyroConf.Id;
            gyroInfo["FS"] = gyroConf.SamplingFrequency;
            gyroInfo["dataPayload"] = gyroConf.PayloadLength;
            gyroInfo["nbSamples"] = gyroConf.NumberOfSamples;
            gaitupInfo["sensor"]["baro"] = new JObject { };
            var baroInfo = gaitupInfo["sensor"]["baro"];
            baroInfo["sensorId"] = baroConf.Id;
            baroInfo["FS"] = baroConf.SamplingFrequency;
            baroInfo["dataPayload"] = baroConf.PayloadLength;
            baroInfo["nbSamples"] = baroConf.NumberOfSamples;

            // Make folder object
            var metaFolder = new JObject { ["type"] = "no.sintef.folder" };
            metaFolder["version"] = 1;
            var userFolder = new JObject { };
            userFolder["info"] = gaitupInfo;


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
        private long _baseFreq;
        internal AccTable(IReadOnlyList<(long, double, double, double)> data, long baseFreq)
        {
            _data = data;
            _baseFreq = baseFreq;
            Name = "accel";
            IsSaved = false;

            bool isWorldSynchronized = false;
            ColInfo timeColInfo = new ColInfo("time", "us");
            string uri = Name + "/" + timeColInfo.Name;

            _timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            var stringUnits = new[]
            {
                new ColInfo("data_accel1", "g"),
                new ColInfo("data_accel2", "g"),
                new ColInfo("data_accel3", "g"),
            };

            foreach (var colInfo in stringUnits)
            {
                uri = Name + "/" + colInfo.Name;
                this.AddColumn(colInfo.Name, GenerateLoader<double>(colInfo), _timeIndex, uri, colInfo.Unit);
            }
        }

        public override Dictionary<string, Array> ReadData()
        {
            var dataDict = new Dictionary<string, Array>();
            var _time = _data.Select(el => el.Item1).ToArray();
            for (var i = 0; i < _time.Length; i++)
            {
                _time[i] = ( _time[i] * 1000000 ) / _baseFreq;
            }

            dataDict.Add("time", _time);
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
            metaTable["units"] = new JArray(GetUnitArr());
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

    public class GyrTable : ImportTableBase, ISaveable
    {
        public bool IsSaved { get; }
        public IReadOnlyList<(long, double, double, double)> _data;
        private long _baseFreq;
        internal GyrTable(IReadOnlyList<(long, double, double, double)> data, long baseFreq)
        {
            _data = data;
            _baseFreq = baseFreq;
            Name = "gyro";
            IsSaved = false;

            bool isWorldSynchronized = false;
            ColInfo timeColInfo = new ColInfo("time", "us");
            string uri = Name + "/" + timeColInfo.Name;

            _timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            var stringUnits = new[]
            {
                new ColInfo("data_gyro1", "deg/s"),
                new ColInfo("data_gyro2", "deg/s"),
                new ColInfo("data_gyro3", "deg/s"),
            };

            foreach (var colInfo in stringUnits)
            {
                uri = Name + "/" + colInfo.Name;
                this.AddColumn(colInfo.Name, GenerateLoader<double>(colInfo), _timeIndex, uri, colInfo.Unit);
            }

        }

        public override Dictionary<string, Array> ReadData()
        {
            var dataDict = new Dictionary<string, Array>();
            var _time = _data.Select(el => el.Item1).ToArray();
            for (var i = 0; i < _time.Length; i++)
            {
                _time[i] = (_time[i] * 1000000) / _baseFreq;
            }

            dataDict.Add("time", _time);
            dataDict.Add("data_gyro1", _data.Select(el => el.Item2).ToArray());
            dataDict.Add("data_gyro2", _data.Select(el => el.Item3).ToArray());
            dataDict.Add("data_gyro3", _data.Select(el => el.Item4).ToArray());
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
            metaTable["units"] = new JArray(GetUnitArr());
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

    public class BaroTable : ImportTableBase, ISaveable
    {
        public bool IsSaved { get; }
        public IReadOnlyList<(long, double, double)> _data;
        private long _baseFreq;
        internal BaroTable(IReadOnlyList<(long, double, double)> data, long baseFreq)
        {
            _data = data;
            _baseFreq = baseFreq;
            Name = "baro";
            IsSaved = false;

            bool isWorldSynchronized = false;
            ColInfo timeColInfo = new ColInfo("time", "us");
            string uri = Name + "/" + timeColInfo.Name;

            _timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            var stringUnits = new[]
            {
                new ColInfo("data_baro1", "hPa"),
                new ColInfo("data_baro2", "C"),
            };

            foreach (var colInfo in stringUnits)
            {
                uri = Name + "/" + colInfo.Name;
                this.AddColumn(colInfo.Name, GenerateLoader<double>(colInfo), _timeIndex, uri, colInfo.Unit);
            }

        }

        public override Dictionary<string, Array> ReadData()
        {
            var dataDict = new Dictionary<string, Array>();
            var _time = _data.Select(el => el.Item1).ToArray();
            for (var i = 0; i < _time.Length; i++)
            {
                _time[i] = (_time[i] * 1000000) / _baseFreq;
            }

            dataDict.Add("time", _time);
            dataDict.Add("data_baro1", _data.Select(el => el.Item2).ToArray());
            dataDict.Add("data_baro2", _data.Select(el => el.Item3).ToArray());
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
            metaTable["units"] = new JArray(GetUnitArr());
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

    public class BleTable : ImportTableBase, ISaveable
    {
        public bool IsSaved { get; }
        public IReadOnlyList<(long, double)> _data;
        private long _baseFreq;
        internal BleTable(IReadOnlyList<(long, double)> data, long baseFreq)
        {
            _data = data;
            _baseFreq = baseFreq;
            Name = "events";
            IsSaved = false;

            bool isWorldSynchronized = false;
            ColInfo timeColInfo = new ColInfo("time", "us");
            string uri = Name + "/" + timeColInfo.Name;

            _timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            timeColInfo = new ColInfo("data_events1", "");
            uri = Name + "/" + timeColInfo.Name;
            this.AddColumn(timeColInfo.Name, GenerateLoader<double>(timeColInfo), _timeIndex, uri, timeColInfo.Unit);

        }

        public override Dictionary<string, Array> ReadData()
        {
            var dataDict = new Dictionary<string, Array>();
            var _time = _data.Select(el => el.Item1).ToArray();
            for (var i = 0; i < _time.Length; i++)
            {
                _time[i] = (_time[i] * 1000000) / _baseFreq;
            }

            dataDict.Add("time", _time);
            dataDict.Add("data_events1", _data.Select(el => el.Item2).ToArray());
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
            metaTable["units"] = new JArray(GetUnitArr());
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

    public class RadioTable : ImportTableBase, ISaveable
    {
        public bool IsSaved { get; }
        public IReadOnlyList<(long, long, double)> _data;
        private long _baseFreq;
        internal RadioTable(IReadOnlyList<(long, long, double)> data, long baseFreq)
        {
            _data = data;
            _baseFreq = baseFreq;
            Name = "radio";
            IsSaved = false;

            bool isWorldSynchronized = false;
            ColInfo timeColInfo = new ColInfo("time", "us");
            string uri = Name + "/" + timeColInfo.Name;

            _timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            timeColInfo = new ColInfo("data_radio1", "");
            uri = Name + "/" + timeColInfo.Name;
            this.AddColumn(timeColInfo.Name, GenerateLoader<long>(timeColInfo), _timeIndex, uri, timeColInfo.Unit);

            timeColInfo = new ColInfo("data_radio2", "");
            uri = Name + "/" + timeColInfo.Name;
            this.AddColumn(timeColInfo.Name, GenerateLoader<double>(timeColInfo), _timeIndex, uri, timeColInfo.Unit);

        }

        public override Dictionary<string, Array> ReadData()
        {
            var dataDict = new Dictionary<string, Array>();
            var _time = _data.Select(el => el.Item1).ToArray();
            for (var i = 0; i < _time.Length; i++)
            {
                _time[i] = (_time[i] * 1000000) / _baseFreq;
            }

            dataDict.Add("time", _time);
            dataDict.Add("data_radio1", _data.Select(el => el.Item2).ToArray());
            dataDict.Add("data_radio2", _data.Select(el => el.Item2).ToArray());
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
            metaTable["units"] = new JArray(GetUnitArr());
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

