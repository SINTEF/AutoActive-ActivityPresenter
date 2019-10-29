using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Implementations;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.Databus.AllocCheck;

[assembly: InternalsVisibleTo("Plugins.Tests")]
namespace SINTEF.AutoActive.Plugins.Import.Csv
{

    [ImportPlugin(".csv")]
    public class CatapultImportPlugin : IImportPlugin
    {
        private AllocTrack mt;
        public CatapultImportPlugin()
        {
            mt = new AllocTrack(this);
        }

        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory, Dictionary<string, object> parameters)
        {
            var importer = new CatapultImporter(readerFactory);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }

        public void GetExtraConfigurationParameters(Dictionary<string, (object, string)> parameters)
        {
        }
    }

    public class CatapultImporter : BaseDataProvider
    {
        private AllocTrack mt;
        internal IReadSeekStreamFactory _readerFactory;
        internal CatapultImporter(IReadSeekStreamFactory readerFactory)
        {
            Name = readerFactory.Name;
            _readerFactory = readerFactory;
            mt = new AllocTrack(this, Name);
        }

        protected override void DoParseFile(Stream s)
        {
            var lines = new List<string>(10);
            using (var sr = new StreamReader(s))
            {
                for (var i = 0; i < lines.Capacity; i++)
                {
                    if (sr.EndOfStream) break;
                    lines.Add(sr.ReadLine());
                }
            }

            var parameters = new Dictionary<string, string>();
            foreach(var line in lines)
            {
                if (!line.Contains("=")) continue;
                var lineSplit = line.Split(new[] {'='}, 2);
                parameters[lineSplit[0]] = lineSplit[1];
            }

            var startTime = 0L;
            if (parameters.TryGetValue("Date", out var date) && parameters.TryGetValue("Time", out var time))
            {
                try
                {
                    startTime = ParseDateTime(date, time);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not parse start time: {ex}");
                }
            }

            AddChild(new CatapultTable(Name+"_table", startTime, _readerFactory, Name + _readerFactory.Extension));
        }

        private long ParseDateTime(string date, string time)
        {
            var dateTime = DateTime.Parse(date + " " + time, CultureInfo.InvariantCulture);
            return TimeFormatter.TimeFromDateTime(dateTime);
        }
    }

    public class CatapultTable : CsvTableBase, ISaveable
    {
        public bool IsSaved { get; }
        private IReadSeekStreamFactory _readerFactory;
        private string _fileName;

        private AllocTrack mt;
        internal CatapultTable(string name, long startTime, IReadSeekStreamFactory readerFactory, string fileName)
        {
            mt = new AllocTrack(this, name);
            Name = name;
            _readerFactory = readerFactory;
            IsSaved = false;
            _fileName = fileName;

            var isWorldSynchronized = startTime != 0L;
            var timeColInfo = new ColInfo("Time", "us");
            var uri = Name + "/" + timeColInfo.Name;
            var timeLoadTask = GenerateLoader<long>(timeColInfo);
            _timeIndex = new TableTimeIndex(timeColInfo.Name, timeLoadTask, isWorldSynchronized, uri, timeColInfo.Unit);
            timeLoadTask.ContinueWith(t => _timeIndex.TransformTime(startTime, 1d));


            var stringUnits = new[]
            {
                new ColInfo("Forward", null),
                new ColInfo("Sideways", null),
                new ColInfo("Up", null),
                new ColInfo("VelDpr", "dpr"),
                new ColInfo("Gyr1", "d/s"),
                new ColInfo("Gyr2", "d/s"),
                new ColInfo("Gyr3", "d/s"),
                new ColInfo("Altitude", null),
                new ColInfo("VelAv", null),
                new ColInfo("HDOP", null),
                new ColInfo("VDOP", null),
                new ColInfo("Longitude", "deg"),
                new ColInfo("Latitude", "deg"),
                new ColInfo("Heartrate", "bps"),
                new ColInfo("Acc", null),
                new ColInfo("Rawvel", null),
            };

            foreach (var colInfo in stringUnits)
            {
                uri = Name + "/" + colInfo.Name;
                this.AddColumn(colInfo.Name, GenerateLoader<float>(colInfo), _timeIndex, uri, colInfo.Unit);
            }
        }

        public override Dictionary<string, Array> ReadData()
        {
            return GenericReadData(new CatapultParser(), _readerFactory);
        }


        public Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            // TODO: give a better name?
            var fileId = "/Import" + "/" + Name + "." + Guid.NewGuid();

            // Make table object
            var metaTable = new JObject { ["type"] = "no.sintef.table" };
            metaTable["attachments"] = new JArray(new object[] { fileId });
            metaTable["units"] = new JArray(GetUnitArr());
            metaTable["is_world_clock"] = _timeIndex.IsSynchronizedToWorldClock;
            metaTable["version"] = 1;

            var userTable = new JObject { };

            var rootTable = new JObject { ["meta"] = metaTable, ["user"] = userTable };

            // Make folder object
            var metaFolder = new JObject { ["type"] = "no.sintef.folder" };
            var userFolder = new JObject { ["full_table"] = rootTable };
            userFolder["filename"] = _fileName;

            // Place objects into root
            root["meta"] = metaFolder;
            root["user"] = userFolder;

            return WriteTable(fileId, writer);
        }
    }

    public class CatapultParser : ICsvParser<CatapultRecord>
    {
        // Make all the arrays needed
        private readonly List<long> _timeData = new List<long>();
        private readonly List<float> _forwardData = new List<float>();
        private readonly List<float> _sidewaysData = new List<float>();
        private readonly List<float> _upData = new List<float>();
        private readonly List<float> _velDprData = new List<float>();
        private readonly List<float> _gyr1Data = new List<float>();
        private readonly List<float> _gyr2Data = new List<float>();
        private readonly List<float> _gyr3Data = new List<float>();
        private readonly List<float> _altitudeData = new List<float>();
        private readonly List<float> _velAvData = new List<float>();
        private readonly List<float> _hdopData = new List<float>();
        private readonly List<float> _vdopData = new List<float>();
        private readonly List<float> _logitudeData = new List<float>();
        private readonly List<float> _latitudeData = new List<float>();
        private readonly List<float> _heartrateData = new List<float>();
        private readonly List<float> _accData = new List<float>();
        private readonly List<float> _rawvelData = new List<float>();

        private AllocTrack mt;
        public CatapultParser()
        {
            mt = new AllocTrack(this);
        }

        public void ConfigureCsvReader(CsvReader csvReader)
        {
            // Configure csv reader
            csvReader.Configuration.ShouldSkipRecord = CheckLine;
            csvReader.Configuration.BadDataFound = null;
            csvReader.Configuration.TrimOptions = CsvHelper.Configuration.TrimOptions.Trim;
            csvReader.Configuration.Delimiter = ",";
            csvReader.Configuration.CultureInfo = new CultureInfo("en-US");
        }

        internal static long ConvHmssToEpochUs(string timeString)
        {
            long hours = 0;
            long minutes;
            long seconds;
            long centiSeconds;
            // Expected character format 'M:S.SS' or 'H:M:S.SS'
            var timeSplit = timeString.Split(':');

            var secondsSplit = timeSplit.Last().Split('.');

            if (timeSplit.Length == 2)
            {
                minutes = long.Parse(timeSplit[0]);
                seconds = long.Parse(secondsSplit[0]);
                centiSeconds = long.Parse(secondsSplit[1]);
            }
            else
            {
                hours = long.Parse(timeSplit[0]);
                minutes = long.Parse(timeSplit[1]);
                seconds = long.Parse(secondsSplit[0]);
                centiSeconds = long.Parse(secondsSplit[1]);
            }

            var epochUs = ((hours * 3600L + minutes * 60L + seconds)* 100L + centiSeconds) * 10000L;
            return epochUs;
        }

        public void ParseRecord(int rowIdx, CatapultRecord rec)
        {
            var time = rec.Stringtime;
            _timeData.Add(ConvHmssToEpochUs(time));

            _forwardData.Add(rec.Forward);
            _sidewaysData.Add(rec.Sideways);
            _upData.Add(rec.Up);
            _velDprData.Add(rec.Vel_dpr);
            _gyr1Data.Add(rec.Gyr1);
            _gyr2Data.Add(rec.Gyr2);
            _gyr3Data.Add(rec.Gyr3);
            _altitudeData.Add(rec.Altitude);
            _velAvData.Add(rec.Vel_av);
            _hdopData.Add(rec.HDOP);
            _vdopData.Add(rec.VDOP);
            _logitudeData.Add(rec.Longitude);
            _latitudeData.Add(rec.Latitude);
            _heartrateData.Add(rec.Heartrate);
            _accData.Add(rec.Acc);
            _rawvelData.Add(rec.Rawvel);
        }

        public Dictionary<string, Array> GetParsedData()
        {
            // Wrap up and store result
            var locData = new Dictionary<string, Array>
            {
                {"Time", _timeData.ToArray()},
                {"Forward", _forwardData.ToArray()},
                {"Sideways", _sidewaysData.ToArray()},
                {"Up", _upData.ToArray()},
                {"VelDpr", _velDprData.ToArray()},
                {"Gyr1", _gyr1Data.ToArray()},
                {"Gyr2", _gyr2Data.ToArray()},
                {"Gyr3", _gyr3Data.ToArray()},
                {"Altitude", _altitudeData.ToArray()},
                {"VelAv", _velAvData.ToArray()},
                {"HDOP", _hdopData.ToArray()},
                {"VDOP", _vdopData.ToArray()},
                {"Longitude", _logitudeData.ToArray()},
                {"Latitude", _latitudeData.ToArray()},
                {"Heartrate", _heartrateData.ToArray()},
                {"Acc", _accData.ToArray()},
                {"Rawvel", _rawvelData.ToArray()}
            };

            
            return locData;
        }

        private readonly List<string> _preHeaderItems = new List<string>();
        private readonly string[] _preHeaderSignatures = { "Logan", "rawFileName=", "From=", "Date=", "Time=", "Athlete=", "EventDescription=" };
        internal bool CheckLine(string[] lines)
        {
            if (!_preHeaderSignatures.Any(signature => lines[0].StartsWith(signature))) return false;

            _preHeaderItems.Add(lines[0]);
            return true;

        }

    }


    public class CatapultRecord
    {

        [Name("Time")]
        public string Stringtime { get; set; }

        [Name("Forward")]
        public float Forward { get; set; }

        [Name("Sideways")]
        public float Sideways { get; set; }

        [Name("Up")]
        public float Up { get; set; }

        [Name("Vel(Dpr)")]
        public float Vel_dpr { get; set; }

        [Name("Gyr1(d/s)")]
        public float Gyr1 { get; set; }

        [Name("Gyr2(d/s)")]
        public float Gyr2 { get; set; }

        [Name("Gyr3(d/s)")]
        public float Gyr3 { get; set; }

        [Name("Altitude")]
        public float Altitude { get; set; }

        [Name("Vel(av)")]
        public float Vel_av { get; set; }

        [Name("HDOP")]
        public float HDOP { get; set; }

        [Name("VDOP")]
        public float VDOP { get; set; }

        [Name("Longitude")]
        public float Longitude { get; set; }

        [Name("Latitude")]
        public float Latitude { get; set; }

        [Name("Heart Rate")]
        public float Heartrate { get; set; }

        [Name("Acc(dpr)")]
        public float Acc { get; set; }

        [Name("Raw Vel.")]
        public float Rawvel { get; set; }

        [Name("GPS Time")]
        public string GPStime { get; set; }

        [Name("GPS Latitude")]
        public string GPSlatitude { get; set; }

        [Name("GPS Longitude")]
        public string GPSlongitude { get; set; }

    }


}