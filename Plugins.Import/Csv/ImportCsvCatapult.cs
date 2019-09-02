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
using SINTEF.AutoActive.Plugins.Import.Csv;
using Newtonsoft.Json.Linq;
using Parquet.Data;
using System.Globalization;

namespace SINTEF.AutoActive.Plugins.Import.Csv.Catapult
{

    [ImportPlugin(".csv")]
    public class CatapultImportPlugin : IImportPlugin
    {
        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory)
        {
            var importer = new CatapultImporter(readerFactory);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }
    }

    // CsvTableBase

    // BaseDataProvider

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
            string columnName = "Time";
            string uri = Name + "/" + columnName;

            var time = new TableTimeIndex(columnName, GenerateLoader<long>(columnName), isWorldSynchronized, uri);

            columnName = "Forward";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Sideways";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Up";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Dpr";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Gyr1";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Gyr2";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Gyr3";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Altitude";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Vel";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "HDOP";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "VDOP";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Longitude";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Latitude";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Heartrate";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Acc";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);

            columnName = "Rawvel";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(columnName), time, uri);
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
            metaTable["units"] = new JArray(new object[] {});
            metaTable["is_world_clock"] = false;
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

            // This stream will be disposed by the sessionWriter
            var ms = new MemoryStream();

            var dataColAndSchema = makeDataColumnAndSchema();

            using (var tableWriter = new Parquet.ParquetWriter(dataColAndSchema.schema, ms))
            {
                //tableWriter.CompressionMethod = Parquet.CompressionMethod.Gzip;

                using (var rowGroup = tableWriter.CreateRowGroup())  // Using construction assure correct storage of final rowGroup details in parquet file
                {
                    foreach (var dataCol in dataColAndSchema.dataColumns)
                    {
                        rowGroup.WriteColumn(dataCol);
                    }
                }
            }

            ms.Position = 0;
            writer.StoreFileId(ms, fileId);

            return true;
        }



    }

    public class CatapultParser : ICsvParser<CatapultRecord>
    {
        // Make all the arrays needed
        private long[] timeData = null;
        private float[] forwardData = null;
        private float[] sidewaysData = null;
        private float[] upData = null;
        private float[] dprData = null;
        private float[] gyr1Data = null;
        private float[] gyr2Data = null;
        private float[] gyr3Data = null;
        private float[] altitudeData = null;
        private float[] velData = null;
        private float[] hdopData = null;
        private float[] vdopData = null;
        private float[] logitudeData = null;
        private float[] latitudeData = null;
        private float[] heartrateData = null;
        private float[] accData = null;
        private float[] rawvelData = null;

        private int lastIdx = 0;

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
            var currLength = timeData?.Length ?? 0;
            if (rowIdx >= currLength)
            {
                var newLength = currLength + 1000;
                Array.Resize(ref timeData, newLength);
                Array.Resize(ref forwardData, newLength);
                Array.Resize(ref sidewaysData, newLength);
                Array.Resize(ref upData, newLength);
                Array.Resize(ref dprData, newLength);
                Array.Resize(ref gyr1Data, newLength);
                Array.Resize(ref gyr2Data, newLength);
                Array.Resize(ref gyr3Data, newLength);
                Array.Resize(ref altitudeData, newLength);
                Array.Resize(ref velData, newLength);
                Array.Resize(ref hdopData, newLength);
                Array.Resize(ref vdopData, newLength);
                Array.Resize(ref logitudeData, newLength);
                Array.Resize(ref latitudeData, newLength);
                Array.Resize(ref heartrateData, newLength);
                Array.Resize(ref accData, newLength);
                Array.Resize(ref rawvelData, newLength);
            }

            //timeData[rowIdx] = rowIdx;
            var time = rec.Stringtime;
            timeData[rowIdx] = ConvHmssToEpochUs(time);

            forwardData[rowIdx] = rec.Forward;
            sidewaysData[rowIdx] = rec.Sideways;
            upData[rowIdx] = rec.Up;
            dprData[rowIdx] = rec.Dpr;
            gyr1Data[rowIdx] = rec.Gyr1;
            gyr2Data[rowIdx] = rec.Gyr2;
            gyr3Data[rowIdx] = rec.Gyr3;
            altitudeData[rowIdx] = rec.Altitude;
            velData[rowIdx] = rec.Vel;
            hdopData[rowIdx] = rec.HDOP;
            vdopData[rowIdx] = rec.VDOP;
            logitudeData[rowIdx] = rec.Longitude;
            latitudeData[rowIdx] = rec.Latitude;
            heartrateData[rowIdx] = rec.Heartrate;
            accData[rowIdx] = rec.Acc;
            rawvelData[rowIdx] = rec.Rawvel;

            lastIdx = rowIdx;
        }

        public Dictionary<string, Array> GetParsedData()
        {
            Dictionary<string, Array> locData = new Dictionary<string, Array>();

            // Wrap up and store result
            var finalLength = lastIdx + 1;
            Array.Resize(ref timeData, finalLength);
            Array.Resize(ref forwardData, finalLength);
            Array.Resize(ref sidewaysData, finalLength);
            Array.Resize(ref upData, finalLength);
            Array.Resize(ref dprData, finalLength);
            Array.Resize(ref gyr1Data, finalLength);
            Array.Resize(ref gyr2Data, finalLength);
            Array.Resize(ref gyr3Data, finalLength);
            Array.Resize(ref altitudeData, finalLength);
            Array.Resize(ref velData, finalLength);
            Array.Resize(ref hdopData, finalLength);
            Array.Resize(ref vdopData, finalLength);
            Array.Resize(ref logitudeData, finalLength);
            Array.Resize(ref latitudeData, finalLength);
            Array.Resize(ref heartrateData, finalLength);
            Array.Resize(ref accData, finalLength);
            Array.Resize(ref rawvelData, finalLength);
            locData.Add("Time", timeData);
            locData.Add("Forward", forwardData);
            locData.Add("Sideways", sidewaysData);
            locData.Add("Up", upData);
            locData.Add("Dpr", dprData);
            locData.Add("Gyr1", gyr1Data);
            locData.Add("Gyr2", gyr2Data);
            locData.Add("Gyr3", gyr3Data);
            locData.Add("Altitude", altitudeData);
            locData.Add("Vel", velData);
            locData.Add("HDOP", hdopData);
            locData.Add("VDOP", vdopData);
            locData.Add("Longitude", logitudeData);
            locData.Add("Latitude", latitudeData);
            locData.Add("Heartrate", heartrateData);
            locData.Add("Acc", accData);
            locData.Add("Rawvel", rawvelData);

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
        public float Dpr { get; set; }

        [Name("Gyr1(d/s)")]
        public float Gyr1 { get; set; }

        [Name("Gyr2(d/s)")]
        public float Gyr2 { get; set; }

        [Name("Gyr3(d/s)")]
        public float Gyr3 { get; set; }

        [Name("Altitude")]
        public float Altitude { get; set; }

        [Name("Vel(av)")]
        public float Vel { get; set; }

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