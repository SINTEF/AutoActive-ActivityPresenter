using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using CsvHelper;
using CsvHelper.Configuration.Attributes;

using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using Parquet.Data;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Table;

namespace SINTEF.AutoActive.Plugins.Import.Csv
{


    [ImportPlugin(".csv")]
    public class CatapultImportPlugin : IImportPlugin
    {
        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory)
        {
            var importer = new CatapultImporter(readerFactory.Name);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }
    }

    public class RememberingCsvReader
    {
        private readonly CsvImporterBase _reader;
        private Dictionary<string, Array> _data = null;

        public RememberingCsvReader(CsvImporterBase reader)
        {
            _reader = reader;
        }

        public RememberingCsvReader(RememberingCsvReader rcr)
        {
            // Make a copy of existing data and reader
            _reader = rcr._reader;
            _data = new Dictionary<string, Array>(rcr._data);
        }

        public void LoadAll()
        {
            if (_data == null)
            {
                _data = _reader.ReadData();
            }
        }

        public T[] LoadColumn<T>(string columnName)
        {
            LoadAll();

            if (_data.TryGetValue(columnName, out var arr))
            {
                return arr as T[];
            }

            throw new ArgumentException($"Couldn't find column {columnName} in csv table");

        }

    }

    public interface ICsvParser<T>
    {
        void ConfigureCsvReader(CsvReader csvReader);

        void ParseRecord(int rowIdx, T record);

        Dictionary<string, Array> GetParsedData();
    }

    public interface ICsvRecord
    {

    }

    public abstract class CsvImporterBase : BaseDataProvider
    {
        protected readonly RememberingCsvReader _reader;

        public CsvImporterBase()
        {
            _reader = new RememberingCsvReader(this);
        }

        // To be impemented in specialization
        protected abstract override void DoParseFile(Stream s);

        public abstract Dictionary<string, Array> ReadData();

        protected static Task<T[]> LoadColumn<T>(RememberingCsvReader reader, string columnName)
        {
            return Task.FromResult(reader.LoadColumn<T>(columnName));
        }

        protected static Task<T[]> GenerateLoader<T>(RememberingCsvReader reader, string columnName)
        {
            return new Task<T[]>(() => LoadColumn<T>(reader, columnName).Result);
        }

        
        public Dictionary<string, Array> GenericReadData<CsvRecord>(ICsvParser<CsvRecord> parser, Stream csvStream)
        {
            // Read data from file 
            try
            {
                csvStream.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(csvStream))
                {
                    // var line = reader.ReadLine();
                    using (var csv = new CsvReader(reader))
                    {
                        parser.ConfigureCsvReader(csv);


                        // Prepare while loop
                        var records = csv.GetRecords<CsvRecord>();
                        var myEnum = records.GetEnumerator();
                        var hasRec = myEnum.MoveNext();
                        int rowCount = 0;   // Number of data rows read
                        while (hasRec)
                        {
                            // Fetch data from record
                            var rec = myEnum.Current;

                            parser.ParseRecord(rowCount, rec);

                            // Prepare for next iteration
                            rowCount++;
                            hasRec = myEnum.MoveNext();
                        }


                    }
                }
            }
            catch (Exception ex)
            {
                var txt = ex.Message;

            }

            return parser.GetParsedData();
        }
    }



    public class CatapultRecord
    {

        [Name("Time")]
        public string Inputtime { get; set; }

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

    public class CatapultParser : ICsvParser<CatapultRecord>
    {
        // Make all the arrays needed
        private long[] timeData = null;
        private float[] forwardData = null;
        private float[] sidewaysData = null;

        private int lastIdx = 0;

        public CatapultParser()
        {

        }

        public void ConfigureCsvReader(CsvReader csvReader)
        {
            // Configure csv reader
            csvReader.Configuration.ShouldSkipRecord = CheckLine;
            csvReader.Configuration.BadDataFound = null;
            csvReader.Configuration.TrimOptions = CsvHelper.Configuration.TrimOptions.Trim;
            csvReader.Configuration.CountBytes = true;
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
            }

            timeData[rowIdx] = rowIdx;
            //timeData[rowCount] = rec.Inputtime;  TODO convert time

            forwardData[rowIdx] = rec.Forward;
            sidewaysData[rowIdx] = rec.Sideways;

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
            locData.Add("Time", timeData);
            locData.Add("Forward", forwardData);
            locData.Add("Sideways", sidewaysData);

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

    public class CatapultImporter : CsvImporterBase
    {
        private Stream _csvStream;

        internal CatapultImporter(string name)
        {
            Name = name;
            _csvStream = null;
        }


        public override Dictionary<string, Array> ReadData()
        {
            return GenericReadData<CatapultRecord>(new CatapultParser(), _csvStream);
        }

#if false
        public override Dictionary<string, Array> ReadData()
        {
            Dictionary<string, Array> locData = new Dictionary<string, Array>();

            // Read data from file 
            try
            {
                _csvStream.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(_csvStream))
                {
                    // var line = reader.ReadLine();
                    using (var csv = new CsvReader(reader))
                    {
                        // Configure csv reader
                        csv.Configuration.ShouldSkipRecord = CheckLine;
                        csv.Configuration.BadDataFound = null;
                        csv.Configuration.TrimOptions = CsvHelper.Configuration.TrimOptions.Trim;
                        csv.Configuration.CountBytes = true;

                        // Make all the arrays needed
                        long[] timeData = null;
                        float[] forwardData = null;
                        float[] sidewaysData = null;

                        // Prepare while loop
                        var records = csv.GetRecords<CatapultRecord>();
                        var myEnum = records.GetEnumerator();
                        var hasRec = myEnum.MoveNext();
                        int rowCount = 0;   // Number of data rows read
                        while (hasRec)
                        {
                            // Fetch data from record
                            var rec = myEnum.Current;

                            var currLength = timeData?.Length ?? 0;
                            if (rowCount >= currLength)
                            {
                                var newLength = currLength + 1000;
                                Array.Resize(ref timeData, newLength);
                                Array.Resize(ref forwardData, newLength);
                                Array.Resize(ref sidewaysData, newLength);
                            }

                            timeData[rowCount] = rowCount;
                            //timeData[rowCount] = rec.Inputtime;  TODO convert time

                            forwardData[rowCount] = rec.Forward;
                            sidewaysData[rowCount] = rec.Sideways;

                            // Prepare for next iteration
                            rowCount++;
                            hasRec = myEnum.MoveNext();
                        }

                        // Wrap up and store result
                        var finalLength = rowCount;
                        Array.Resize(ref timeData, finalLength);
                        Array.Resize(ref forwardData, finalLength);
                        Array.Resize(ref sidewaysData, finalLength);
                        locData.Add("Time", timeData);
                        locData.Add("Forward", forwardData);
                        locData.Add("Sideways", sidewaysData);

                    }
                }
            }
            catch (Exception ex)
            {
                var txt = ex.Message;

            }

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
#endif

        //private ArchiveTableInformation _tableInformation = new ArchiveTableInformation();
        protected override void DoParseFile(Stream s)
        {
            _csvStream = s;

            // AddChild(this);

            bool isWorldSynchronized = false;
            string columnName = "Time";
            string uri = Name + "/" + columnName;

            var time = new TableTimeIndex(columnName, GenerateLoader<long>(_reader, columnName), isWorldSynchronized, uri);

            columnName = "Forward";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(_reader, columnName), time, uri);

            columnName = "Sideways";
            uri = Name + "/" + columnName;
            this.AddColumn(columnName, GenerateLoader<float>(_reader, columnName), time, uri);

            // Todo register more columns

        }
    }
}