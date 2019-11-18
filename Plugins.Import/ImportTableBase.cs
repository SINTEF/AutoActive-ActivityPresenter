using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Implementations;
using Parquet.Data;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using System.IO;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.AllocCheck;

namespace SINTEF.AutoActive.Plugins.Import
{

    public class ColInfo
    {
        public string Name { get; }
        public string Unit { get; }

        public ColInfo(string name, string unit)
        {
            Name = name;
            Unit = unit;
        }
    }

    public class RememberingFullTableReader
    {
        private readonly ImportTableBase _importBase;
        internal Dictionary<string, Array> _data = null;

        private readonly AllocTrack mt;
        public RememberingFullTableReader(ImportTableBase importBase)
        {
            mt = new AllocTrack(this);
            _importBase = importBase;
        }

        public RememberingFullTableReader(RememberingFullTableReader rftr)
        {
            mt = new AllocTrack(this);
            // Make a copy of existing data and reader
            _importBase = rftr._importBase;

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
                _data = _importBase.ReadData();
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
        protected RememberingFullTableReader _reader;
        protected List<ColInfo> _colInfos = new List<ColInfo>();

        protected ImportTableBase()
        {
            _reader = new RememberingFullTableReader(this);
        }

        public abstract Dictionary<string, Array> ReadData();

        protected Task<T[]> LoadColumn<T>(string columnName)
        {
            return Task.FromResult(_reader.LoadColumn<T>(columnName));
        }

        protected Task<T[]> GenerateLoader<T>(ColInfo colInfo)
        {
            _colInfos.Add(colInfo);
            return new Task<T[]>(() => LoadColumn<T>(colInfo.Name).Result);
        }

        protected DataColumnAndSchema makeDataColumnAndSchema()
        {
            // Make a copy of the Remembering reader that later can be discarded
            // This to avoid to read in all tables in memory at the same time.
            var fullReader = new RememberingFullTableReader(_reader);
            fullReader.LoadAll();

            var dataDict = fullReader.getData();

            var fields = new List<Field>();
            var datacols = new List<DataColumn>();

            var numCol = _colInfos.Count;
            for (var i = 0; i < numCol; i++)
            {
                var colInfo = _colInfos[i];
                var dataName = colInfo.Name;
                var dataArr = dataDict[dataName];
                DataColumn column;
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
                    default:
                        continue;
                }
                fields.Add(column.Field);
                datacols.Add(column);
            }

            return new DataColumnAndSchema(datacols, new Schema(fields));
        }

        public Task<bool> WriteTable(string fileId, ISessionWriter writer)
        {
            // This stream will be disposed by the sessionWriter
            var ms = new MemoryStream();

            var dataColAndSchema = makeDataColumnAndSchema();

            using (var tableWriter = new Parquet.ParquetWriter(dataColAndSchema.Schema, ms))
            {
                //tableWriter.CompressionMethod = Parquet.CompressionMethod.Gzip;

                using (var rowGroup = tableWriter.CreateRowGroup())  // Using construction assure correct storage of final rowGroup details in parquet file
                {
                    foreach (var dataCol in dataColAndSchema.DataColumns)
                    {
                        rowGroup.WriteColumn(dataCol);
                    }
                }
            }

            ms.Position = 0;
            writer.StoreFileId(ms, fileId);

            return Task.FromResult(true);
        }

        protected string[] GetUnitArr()
        {
            // Make unit table
            var numCol = _colInfos.Count;
            var units = new string[numCol];
            for (var i = 0; i < numCol; i++)
            {
                units[i] = _colInfos[i].Unit;
            }

            return units;
        }
}

public class DataColumnAndSchema
    {
        public readonly List<DataColumn> DataColumns;
        public readonly Schema Schema;
        public DataColumnAndSchema(List<DataColumn> dataCols, Schema sch)
        {
            DataColumns = dataCols;
            Schema = sch;
        }
    }


}