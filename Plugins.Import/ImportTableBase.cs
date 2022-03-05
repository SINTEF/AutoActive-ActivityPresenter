using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Implementations;
using Parquet.Data;
using System.IO;
using System.Linq;
using SINTEF.AutoActive.Databus.Interfaces;

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
        public Dictionary<string, Array> Data { get; private set; }

        public RememberingFullTableReader(ImportTableBase importBase)
        {
            _importBase = importBase;
        }

        public RememberingFullTableReader(RememberingFullTableReader rftr)
        {
            // Make a copy of existing data and reader
            _importBase = rftr._importBase;

            if (rftr.Data != null)
            {
                Data = new Dictionary<string, Array>(rftr.Data);
            }
        }

        public void LoadAll()
        {
            if (Data == null)
            {
                Data = _importBase.ReadData();
            }
        }

        public T[] LoadColumn<T>(string columnName)
        {
            LoadAll();

            if (Data.TryGetValue(columnName, out var arr))
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

        protected DataColumnAndSchema MakeDataColumnAndSchema()
        {
            // Make a copy of the Remembering reader that later can be discarded
            // This to avoid to read in all tables in memory at the same time.
            var fullReader = new RememberingFullTableReader(_reader);
            fullReader.LoadAll();

            var dataDict = fullReader.Data;

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
                    case bool[] _:
                        column = new DataColumn(new DataField<bool>(dataName), dataArr);
                        break;
                    case byte[] _:
                        column = new DataColumn(new DataField<byte>(dataName), dataArr);
                        break;
                    case int[] _:
                        column = new DataColumn(new DataField<int>(dataName), dataArr);
                        break;
                    case long[] _:
                        column = new DataColumn(new DataField<long>(dataName), dataArr);
                        break;
                    case float[] _:
                        column = new DataColumn(new DataField<float>(dataName), dataArr);
                        break;
                    case double[] _:
                        column = new DataColumn(new DataField<double>(dataName), dataArr);
                        break;
                    case string[] _:
                        column = new DataColumn(new DataField<string>(dataName), dataArr);
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

            var dataColAndSchema = MakeDataColumnAndSchema();

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

        protected string[] GetUnitArray()
        {
            return _colInfos.Select(c => c.Unit).ToArray();
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