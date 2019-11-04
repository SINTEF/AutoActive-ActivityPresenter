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
using SINTEF.AutoActive.Databus.AllocCheck;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.Import.Gaitup
{
    [ImportPlugin(".bin")]
    public class ImportGaitupPlugin : IBatchImportPlugin
    {
        private AllocTrack mt;
        private List<GaitupData> _sessionData;

        private bool _isFirst;
        private Dictionary<IReadSeekStreamFactory, Task> _gaitupMap  = new Dictionary<IReadSeekStreamFactory, Task>();
        private Barrier _barrier;
        private Mutex _transactionMutex;
        private readonly EventWaitHandle _waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        public ImportGaitupPlugin()
        {
            mt = new AllocTrack(this);
        }


        public void StartTransaction(List<IReadSeekStreamFactory> streamFactories)
        {
            _sessionData = new List<GaitupData>();
            _waitHandle.Reset();

            _transactionMutex = new Mutex();
            _isFirst = true;
            _barrier = new Barrier(streamFactories.Count);

            foreach (var readerFactory in streamFactories)
            {
                var task = ParseFile(readerFactory);
                _gaitupMap[readerFactory] = task;
                task.Start();
            }
        }

        private Task ParseFile(IReadSeekStreamFactory readerFactory)
        {
            return new Task(() =>
            {
                var streamTask = readerFactory.GetReadStream();
                streamTask.Wait();
                var stream = streamTask.Result;
                var name = readerFactory.Name;
                var fileName = name + readerFactory.Extension;
                var parser = new GaitupParser.GaitupParser(stream, name, fileName);

                parser.ParseFile();
                lock (_transactionMutex)
                {
                    _sessionData.Add(parser.GetData());
                }

                _barrier.SignalAndWait(TimeSpan.FromSeconds(5));

                _waitHandle.Set();
            });
        }

        public void EndTransaction()
        {
        }

        public Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory, Dictionary<string, object> parameters)
        {
            if (!_waitHandle.WaitOne(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Could not find all gaitup files in session.");
            }
            
            lock (_transactionMutex)
            {
                if (!_isFirst) return Task.FromResult<IDataProvider>(null);

                _isFirst = false;
                Debug.WriteLine($"Start GaitUpSync {readerFactory.Name}: {_sessionData.Count}");
                var synchronizer = new GaitupSynchronizer(_sessionData);
                synchronizer.Synchronize();
                synchronizer.CropSets();
                return Task.FromResult<IDataProvider>(new GaitupSessionImporter(_sessionData, parameters["Name"] as string));
            }
        }

        public void GetExtraConfigurationParameters(Dictionary<string, (object, string)> parameters)
        {
        }
    }

    public class GaitupSessionImporter : BaseDataProvider
    {
        private AllocTrack mt;
        public GaitupSessionImporter(List<GaitupData> sessionData, string name)
        {
            mt = new AllocTrack(this, name);
            Name = name;
            AddChild(new GaitupTop(sessionData));
        }

        protected override void DoParseFile(Stream s)
        {
            // Files already parsed and synced ... Nothing more to do
        }

    }

    public class GaitupTop : BaseDataStructure, ISaveable
    {
        private AllocTrack mt;
        public bool IsSaved { get; }
        private List<GaitupData> _sessionData = null;
        public GaitupTop(List<GaitupData> sessionData)
        {
            Name = "Gaitup";
            mt = new AllocTrack(this, Name);
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
        private AllocTrack mt;
        public bool IsSaved { get; }
        private readonly GaitupData _data;
        public GaitupFolder(GaitupData data)
        {
            _data = data;
            Name = "sensor" + data.Config.Name;
            mt = new AllocTrack(this, Name);

            IsSaved = false;
            if(data.Accelerometer.Count > 0) AddChild(new AccTable(data.Accelerometer, data.Config.Frequency));
            if (data.Gyro.Count > 0) AddChild(new GyrTable(data.Gyro, data.Config.Frequency));
            if (data.Barometer.Count > 0) AddChild(new BaroTable(data.Barometer, data.Config.Frequency));
            if (data.Ble.Count > 0) AddChild(new BleTable(data.Ble, data.Config.Frequency));
            if (data.Radio.Count > 0) AddChild(new RadioTable(data.Radio, data.Config.Frequency));
        }

        public JObject MakeDate(DateTime date)
        {
            return new JObject
            {
                ["Year"] = date.Year,
                ["Month"] = date.Month,
                ["Day"] = date.Day,
                ["Hour"] = date.Hour,
                ["Minute"] = date.Minute,
                ["Seconds"] = date.Second
            };
        }

        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            var sensorConf = _data.Config.Sensor;
            var conf = _data.Config;
            var accConf = _data.Config.Accelerometer;
            var gyroConf = _data.Config.Gyro;
            var baroConf = _data.Config.Barometer;
            var gaitupInfo = new JObject
            {
                ["deviceId"] = sensorConf.DeviceId,
                ["deviceType"] = sensorConf.DeviceType,
                ["bodyLocation"] = sensorConf.BodyLocation,
                ["firmawareVersion"] = "" + sensorConf.Version + "." + sensorConf.MajorVersion + "." +
                                       sensorConf.MinorVersion,
                ["baseFrequency"] = conf.Frequency,
                ["measureID"] = conf.MeasureId,
                ["filename"] = conf.FileName,
                ["startDate"] = MakeDate(conf.StartDate),
                ["stopDate"] = MakeDate(conf.StopDate),
                ["sensor"] = new JObject { },
                ["sensor"] =
                {
                    ["accel"] = new JObject
                    {
                        ["scale"] = accConf.Scale,
                        ["sensorId"] = accConf.Id,
                        ["FS"] = accConf.SamplingFrequency,
                        ["dataPayload"] = accConf.PayloadLength,
                        ["nbSamples"] = accConf.NumberOfSamples
                    }
                },
                ["sensor"] =
                {
                    ["gyro"] = new JObject
                    {
                        ["scale"] = gyroConf.Scale,
                        ["sensorId"] = gyroConf.Id,
                        ["FS"] = gyroConf.SamplingFrequency,
                        ["dataPayload"] = gyroConf.PayloadLength,
                        ["nbSamples"] = gyroConf.NumberOfSamples
                    }
                },
                ["sensor"] =
                {
                    ["baro"] = new JObject
                    {
                        ["sensorId"] = baroConf.Id,
                        ["FS"] = baroConf.SamplingFrequency,
                        ["dataPayload"] = baroConf.PayloadLength,
                        ["nbSamples"] = baroConf.NumberOfSamples
                    }
                }
            };

            // Make folder object
            var metaFolder = new JObject {["type"] = "no.sintef.folder", ["version"] = 1};
            var userFolder = new JObject {["info"] = gaitupInfo};

            // Place objects into root
            root["meta"] = metaFolder;
            root["user"] = userFolder;

            return true;
        }
    }


    public class AccTable : ImportTableBase, ISaveable
    {
        private AllocTrack mt;
        public bool IsSaved { get; }
        public IReadOnlyList<(long, double, double, double)> _data;
        private long _baseFreq;
        internal AccTable(IReadOnlyList<(long, double, double, double)> data, long baseFreq)
        {
            _data = data;
            _baseFreq = baseFreq;
            Name = "accel";
            mt = new AllocTrack(this, Name);
            IsSaved = false;

            var isWorldSynchronized = false;
            var timeColInfo = new ColInfo("time", "us");
            var uri = Name + "/" + timeColInfo.Name;

            var timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            var stringUnits = new[]
            {
                new ColInfo("data_accel1", "g"),
                new ColInfo("data_accel2", "g"),
                new ColInfo("data_accel3", "g"),
            };

            foreach (var colInfo in stringUnits)
            {
                uri = Name + "/" + colInfo.Name;
                this.AddColumn(colInfo.Name, GenerateLoader<double>(colInfo), timeIndex, uri, colInfo.Unit);
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
            metaTable["is_world_clock"] = DataPoints.First().Time.IsSynchronizedToWorldClock;
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
        private AllocTrack mt;
        public bool IsSaved { get; }
        public IReadOnlyList<(long, double, double, double)> _data;
        private long _baseFreq;
        internal GyrTable(IReadOnlyList<(long, double, double, double)> data, long baseFreq)
        {
            _data = data;
            _baseFreq = baseFreq;
            Name = "gyro";
            mt = new AllocTrack(this, Name);
            IsSaved = false;

            bool isWorldSynchronized = false;
            ColInfo timeColInfo = new ColInfo("time", "us");
            string uri = Name + "/" + timeColInfo.Name;

            var timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            var stringUnits = new[]
            {
                new ColInfo("data_gyro1", "deg/s"),
                new ColInfo("data_gyro2", "deg/s"),
                new ColInfo("data_gyro3", "deg/s"),
            };

            foreach (var colInfo in stringUnits)
            {
                uri = Name + "/" + colInfo.Name;
                this.AddColumn(colInfo.Name, GenerateLoader<double>(colInfo), timeIndex, uri, colInfo.Unit);
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
            metaTable["is_world_clock"] = DataPoints.First().Time.IsSynchronizedToWorldClock;
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
        private AllocTrack mt;
        public bool IsSaved { get; }
        public IReadOnlyList<(long, double, double)> _data;
        private long _baseFreq;
        internal BaroTable(IReadOnlyList<(long, double, double)> data, long baseFreq)
        {
            _data = data;
            _baseFreq = baseFreq;
            Name = "baro";
            mt = new AllocTrack(this, Name);
            IsSaved = false;

            bool isWorldSynchronized = false;
            ColInfo timeColInfo = new ColInfo("time", "us");
            string uri = Name + "/" + timeColInfo.Name;

            var timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            var stringUnits = new[]
            {
                new ColInfo("data_baro1", "hPa"),
                new ColInfo("data_baro2", "C"),
            };

            foreach (var colInfo in stringUnits)
            {
                uri = Name + "/" + colInfo.Name;
                this.AddColumn(colInfo.Name, GenerateLoader<double>(colInfo), timeIndex, uri, colInfo.Unit);
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
            metaTable["is_world_clock"] = DataPoints.First().Time.IsSynchronizedToWorldClock;
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
        private AllocTrack mt;
        public bool IsSaved { get; }
        public IReadOnlyList<(long, double)> _data;
        private long _baseFreq;
        internal BleTable(IReadOnlyList<(long, double)> data, long baseFreq)
        {
            _data = data;
            _baseFreq = baseFreq;
            Name = "events";
            mt = new AllocTrack(this, Name);
            IsSaved = false;

            bool isWorldSynchronized = false;
            ColInfo timeColInfo = new ColInfo("time", "us");
            string uri = Name + "/" + timeColInfo.Name;

            var timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            timeColInfo = new ColInfo("data_events1", "");
            uri = Name + "/" + timeColInfo.Name;
            this.AddColumn(timeColInfo.Name, GenerateLoader<double>(timeColInfo), timeIndex, uri, timeColInfo.Unit);

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
            metaTable["is_world_clock"] = DataPoints.First().Time.IsSynchronizedToWorldClock;
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
        private AllocTrack mt;
        public bool IsSaved { get; }
        public IReadOnlyList<(long, long, double)> _data;
        private long _baseFreq;
        internal RadioTable(IReadOnlyList<(long, long, double)> data, long baseFreq)
        {
            _data = data;
            _baseFreq = baseFreq;
            Name = "radio";
            mt = new AllocTrack(this, Name);
            IsSaved = false;

            bool isWorldSynchronized = false;
            ColInfo timeColInfo = new ColInfo("time", "us");
            string uri = Name + "/" + timeColInfo.Name;

            var timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            timeColInfo = new ColInfo("data_radio1", "");
            uri = Name + "/" + timeColInfo.Name;
            this.AddColumn(timeColInfo.Name, GenerateLoader<long>(timeColInfo), timeIndex, uri, timeColInfo.Unit);

            timeColInfo = new ColInfo("data_radio2", "");
            uri = Name + "/" + timeColInfo.Name;
            this.AddColumn(timeColInfo.Name, GenerateLoader<double>(timeColInfo), timeIndex, uri, timeColInfo.Unit);

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
            dataDict.Add("data_radio2", _data.Select(el => el.Item3).ToArray());
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
            metaTable["is_world_clock"] = DataPoints.First().Time.IsSynchronizedToWorldClock;
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

