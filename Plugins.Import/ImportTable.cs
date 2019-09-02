using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Implementations;
using Parquet.Data;

namespace SINTEF.AutoActive.Plugins.Import
{

    public class RememberingFullTableReader
    {
        private readonly ImportTableBase _reader;
        internal Dictionary<string, Array> _data = null;

        public RememberingFullTableReader(ImportTableBase reader)
        {
            _reader = reader;
        }

        public RememberingFullTableReader(RememberingFullTableReader rftr)
        {
            // Make a copy of existing data and reader
            _reader = rftr._reader;

            if (rftr._data != null)
            {
                _data = new Dictionary<string, Array>(rftr._data);
            }
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

    
    public abstract class ImportTableBase : BaseDataStructure
    {
        protected readonly RememberingFullTableReader _reader;

        public ImportTableBase()
        {
            _reader = new RememberingFullTableReader(this);
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

        protected DataColumnAndSchema makeDataColumnAndSchema()
        {
            // Make a copy of the Remembering reader that later can be discarded
            // This to avoid to read in all tables in memory at the same time.
            var fullReader = new RememberingFullTableReader(_reader);
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