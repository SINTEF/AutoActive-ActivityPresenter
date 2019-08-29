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


    public class RememberingCsvReader
    {
        private readonly CsvTableBase _reader;
        private Dictionary<string, Array> _data = null;

        public RememberingCsvReader(CsvTableBase reader)
        {
            _reader = reader;
        }

        public RememberingCsvReader(RememberingCsvReader rcr)
        {
            // Make a copy of existing data and reader
            _reader = rcr._reader;
            _data = new Dictionary<string, Array>(rcr._data);
        }

        public Dictionary<string, Array> getData()
        {
            return _data;
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

    
    public abstract class CsvTableBase : BaseDataStructure
    {
        protected readonly RememberingCsvReader _reader;

        public CsvTableBase()
        {
            _reader = new RememberingCsvReader(this);
        }

        public abstract Dictionary<string, Array> ReadData();

        protected Task<T[]> LoadColumn<T>(string columnName)
        {
            return Task.FromResult(_reader.LoadColumn<T>(columnName));
        }

        protected Task<T[]> GenerateLoader<T>(string columnName)
        {
            return new Task<T[]>(() => LoadColumn<T>(columnName).Result);
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

        public DataColumnAndSchema makeDataColumnAndSchema()
        {
            // Make a copy of the Remembering reader that later can be discarded
            // This to avoid to read in all tables in memory at the same time.
            var fullReader = new RememberingCsvReader(_reader);
            fullReader.LoadAll();

            var dataDict = fullReader.getData();

            List<Field> fields = new List<Field>();
            List<DataColumn> datacols = new List<DataColumn>();

            foreach (KeyValuePair<string, Array> dataEntry in dataDict)
            {
                var dataArr = dataEntry.Value;
                var dataName = dataEntry.Key;
                DataColumn column = null;

                switch (dataArr)
                {
                    case bool[] arr:
                        column = new DataColumn(new DataField<bool>(dataName), dataArr);
                        break;
                    case byte[] arr:
                        column = new DataColumn(new DataField<byte>(dataName), dataArr);
                        break;
                    case int[] arr:
                        column = new DataColumn(new DataField<int>(dataName), dataArr);
                        break;
                    case long[] arr:
                        column = new DataColumn(new DataField<long>(dataName), dataArr);
                        break;
                    case float[] arr:
                        column = new DataColumn(new DataField<float>(dataName), dataArr);
                        break;
                    case double[] arr:
                        column = new DataColumn(new DataField<double>(dataName), dataArr);
                        break;
                }
                fields.Add(column.Field);
                datacols.Add(column);
            }

            var schema = new Schema(fields);
            var dcas = new DataColumnAndSchema(datacols, schema);
            return dcas;
        }

    }

    public class DataColumnAndSchema
    {
        public readonly List<DataColumn> dataColumns;
        public readonly Schema schema;
        public DataColumnAndSchema(List<DataColumn> dataCols, Schema sch)
        {
            dataColumns = dataCols;
            schema = sch;
        }
    }


}