using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Implementations;
using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.Plugins.Import.Csv.Catapult
{

    [ImportPlugin(".csv")]
    public class CatapultImportPlugin : IImportPlugin
    {
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
        internal IReadSeekStreamFactory _readerFactory;
        internal CatapultImporter(IReadSeekStreamFactory readerFactory)
        {
            Name = readerFactory.Name;
            _readerFactory = readerFactory;
        }

        protected override void DoParseFile(Stream s)
        {
            AddChild(new CatapultTable(Name+"_table", _readerFactory, Name + _readerFactory.Extension));
        }
    }

    public class CatapultTable : CsvTableBase, ISaveable
    {
        public bool IsSaved { get; }
        private IReadSeekStreamFactory _readerFactory;
        private string _fileName;
        internal CatapultTable(string name, IReadSeekStreamFactory readerFactory, string fileName)
        {
            Name = name;
            _readerFactory = readerFactory;
            IsSaved = false;
            _fileName = fileName;

            bool isWorldSynchronized = false;
            var timeColInfo = new ColInfo("Time", "us");
            var uri = Name + "/" + timeColInfo.Name;
            _timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);


            var stringUnits = new[]
            {
                new ColInfo("Forward", "?"),
                new ColInfo("Sideways", "?"),
                new ColInfo("Up", "?"),
                new ColInfo("VelDpr", "dpr"),
                new ColInfo("Gyr1", "d/s"),
                new ColInfo("Gyr2", "d/s"),
                new ColInfo("Gyr2", "d/s"),
                new ColInfo("Altitude", "?"),
                new ColInfo("VelAv", "?"),
                new ColInfo("HDOP", "?"),
                new ColInfo("VDOP", "?"),
                new ColInfo("Longitude", "deg"),
                new ColInfo("Latitude", "deg"),
                new ColInfo("Heartrate", "bps"),
                new ColInfo("Acc", "?"),
                new ColInfo("Rawvel", "?"),
            };

            foreach (var colInfo in stringUnits)
            {
                uri = Name + "/" + colInfo.Name;
                this.AddColumn(colInfo.Name, GenerateLoader<float>(colInfo), _timeIndex, uri, colInfo.Unit);
            }
        }

        public override Dictionary<string, Array> ReadData()
        {
            return GenericReadData<CatapultRecord>(new CatapultParser(), _readerFactory);
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

            var rootTable = new JObject { ["meta"] = metaTable, ["user"] = userTable };

            // Make folder object
            var metaFolder = new JObject { ["type"] = "no.sintef.folder" };
            var userFolder = new JObject { ["full_table"] = rootTable };
            userFolder["filename"] = _fileName;

            // Place objects into root
            root["meta"] = metaFolder;
            root["user"] = userFolder;

            bool result = await WriteTable(fileId, writer);
            return result;

        }
    }

    public class CatapultParser : ICsvParser<CatapultRecord>
    {
        // Make all the arrays needed
        private List<long> timeData = new List<long>();
        private List<float> forwardData = new List<float>();
        private List<float> sidewaysData = new List<float>();
        private List<float> upData = new List<float>();
        private List<float> velDprData = new List<float>();
        private List<float> gyr1Data = new List<float>();
        private List<float> gyr2Data = new List<float>();
        private List<float> gyr3Data = new List<float>();
        private List<float> altitudeData = new List<float>();
        private List<float> velAvData = new List<float>();
        private List<float> hdopData = new List<float>();
        private List<float> vdopData = new List<float>();
        private List<float> logitudeData = new List<float>();
        private List<float> latitudeData = new List<float>();
        private List<float> heartrateData = new List<float>();
        private List<float> accData = new List<float>();
        private List<float> rawvelData = new List<float>();

        public void ConfigureCsvReader(CsvReader csvReader)
        {
            // Configure csv reader
            csvReader.Configuration.ShouldSkipRecord = CheckLine;
            csvReader.Configuration.BadDataFound = null;
            csvReader.Configuration.TrimOptions = CsvHelper.Configuration.TrimOptions.Trim;
        }

        private long ConvHmssToEpochUs(string timeString)
        {
            long epochUs = 0;
            int h, m;
            float s;
            // Expected character format 'M:S.SS' or 'H:M:S.SS'
            string[] c_split = timeString.Split(':');

            if(c_split.Length == 2)
            {
                h = 0;
                Int32.TryParse(c_split[0], out m);
                Single.TryParse(c_split[1], out s);
            }
            else
            {
                Int32.TryParse(c_split[0], out h);
                Int32.TryParse(c_split[1], out m);
                Single.TryParse(c_split[2], out s);
            }
            epochUs = (long)(((h * 3600) + (m * 60) + s) * 1000000);
            return epochUs;
        }

        public void ParseRecord(int rowIdx, CatapultRecord rec)
        {
            var time = rec.Stringtime;
            timeData.Add(ConvHmssToEpochUs(time));

            forwardData.Add(rec.Forward);
            sidewaysData.Add(rec.Sideways);
            upData.Add(rec.Up);
            velDprData.Add(rec.Vel_dpr);
            gyr1Data.Add(rec.Gyr1);
            gyr2Data.Add(rec.Gyr2);
            gyr3Data.Add(rec.Gyr3);
            altitudeData.Add(rec.Altitude);
            velAvData.Add(rec.Vel_av);
            hdopData.Add(rec.HDOP);
            vdopData.Add(rec.VDOP);
            logitudeData.Add(rec.Longitude);
            latitudeData.Add(rec.Latitude);
            heartrateData.Add(rec.Heartrate);
            accData.Add(rec.Acc);
            rawvelData.Add(rec.Rawvel);
        }

        public Dictionary<string, Array> GetParsedData()
        {
            Dictionary<string, Array> locData = new Dictionary<string, Array>();

            // Wrap up and store result
            locData.Add("Time", timeData.ToArray());
            locData.Add("Forward", forwardData.ToArray());
            locData.Add("Sideways", sidewaysData.ToArray());
            locData.Add("Up", upData.ToArray());
            locData.Add("VelDpr", velDprData.ToArray());
            locData.Add("Gyr1", gyr1Data.ToArray());
            locData.Add("Gyr2", gyr2Data.ToArray());
            locData.Add("Gyr3", gyr3Data.ToArray());
            locData.Add("Altitude", altitudeData.ToArray());
            locData.Add("VelAv", velAvData.ToArray());
            locData.Add("HDOP", hdopData.ToArray());
            locData.Add("VDOP", vdopData.ToArray());
            locData.Add("Longitude", logitudeData.ToArray());
            locData.Add("Latitude", latitudeData.ToArray());
            locData.Add("Heartrate", heartrateData.ToArray());
            locData.Add("Acc", accData.ToArray());
            locData.Add("Rawvel", rawvelData.ToArray());

            return locData;
        }

        private List<string> _preHeaderItems = new List<string>();
        private readonly string[] _preHeaderSignatures = { "Logan", "rawFileName=", "From=", "Date=", "Time=", "Athlete=", "EventDescription=" };
        internal bool CheckLine(string[] l)
        {
            foreach (string signature in _preHeaderSignatures)
            {
                if (l[0].StartsWith(signature))
                {
                    _preHeaderItems.Add(l[0]);
                    return true;
                }
            }
            return false;
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