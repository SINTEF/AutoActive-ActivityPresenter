using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using Parquet;
using Parquet.Data;
using SINTEF.AutoActive.Archive;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Plugins.ArchivePlugins.Table
{
    internal class RememberingParquetReader
    {
        private readonly ParquetReader _reader;
        public RememberingParquetReader(ParquetReader reader)
        {
            _reader = reader;
        }

        public Schema Schema => _reader.Schema;

        public Parquet.Data.Rows.Table ReadAsTable() => _reader.ReadAsTable();

        private readonly Dictionary<DataField, Array> _data = new Dictionary<DataField, Array>();

        public T[] LoadColumn<T>(DataField column)
        {
            if (_data.TryGetValue(column, out var arr))
            {
                return arr as T[];
            }

            // Find the datafield we want to use
            var dataField = Array.Find(_reader.Schema.GetDataFields(), field => field.Name == column.Name);
            if (dataField == null) throw new ArgumentException($"Couldn't find column {column.Name} in table");

            T[] data = null;
            // Read the data pages
            for (var page = 0; page < _reader.RowGroupCount; page++)
            {
                // TODO: Do this asynchronously?
                var pageReader = _reader.OpenRowGroupReader(page);
                var dataColumn = pageReader.ReadColumn(dataField);
                var prevLength = data?.Length ?? 0;
                Array.Resize(ref data, prevLength + dataColumn.Data.Length);
                Array.Copy(dataColumn.Data, 0, data, prevLength, dataColumn.Data.Length);
            }

            _data[column] = data;

            return data;
        }
    }

    public class ArchiveTable : ArchiveStructure, ISaveable
    {
        public override string Type => "no.sintef.table";
        private readonly ZipEntry _zipEntry;
        private readonly RememberingParquetReader _reader;
        private readonly Archive.Archive _archive;

        internal ArchiveTable(JObject json, Archive.Archive archive, ArchiveTableInformation tableInformation) :
            base(json)
        {
            IsSaved = true;
            _archive = archive;
            _zipEntry = tableInformation.ZipEntry;

            if (tableInformation.Time == null) throw new ArgumentException("Table does not have a column named 'Time'");

            var streamTask = archive.OpenFile(_zipEntry);
            streamTask.Wait();
            using (var reader = new ParquetReader(streamTask.Result))
            {
                _reader = new RememberingParquetReader(reader);
            }

            AddColumns(tableInformation);
        }

        public ArchiveTable(JObject json, ParquetReader reader, ArchiveTableInformation tableInformation, string name) :
            base(json)
        {
            Name = name;
            _reader = new RememberingParquetReader(reader);
            IsSaved = false;

            AddColumns(tableInformation);
        }


        void AddColumns(ArchiveTableInformation tableInformation)
        {
            // TODO(sigurdal): Handle nullable data? column.HasNulls
            var timeInfo = tableInformation.Time;
            var time = new TableTimeIndex(timeInfo.Name, GenerateLoader<long>(_reader, timeInfo), false);

            foreach (var column in tableInformation.Columns)
            {
                if (column.HasNulls)
                {
                    throw new NotImplementedException("Nullable columns are not yet implemented.");
                }

                switch (column.DataType)
                {
                    case DataType.Boolean:
                        this.AddColumn(column.Name, GenerateLoader<bool>(_reader, column), time);
                        break;
                    case DataType.Byte:
                        this.AddColumn(column.Name, GenerateLoader<byte>(_reader, column), time);
                        break;
                    case DataType.Int32:
                        this.AddColumn(column.Name, GenerateLoader<int>(_reader, column), time);
                        break;
                    case DataType.Int64:
                        this.AddColumn(column.Name, GenerateLoader<long>(_reader, column), time);
                        break;
                    case DataType.Float:
                        this.AddColumn(column.Name, GenerateLoader<float>(_reader, column), time);
                        break;
                    case DataType.Double:
                        this.AddColumn(column.Name, GenerateLoader<double>(_reader, column), time);
                        break;

                    default:
                        throw new InvalidOperationException($"Cannot read {column.DataType} columns");
                }
            }
        }

        private static Task<T[]> LoadColumn<T>(RememberingParquetReader reader, DataField column)
        {
            return Task.FromResult(reader.LoadColumn<T>(column));
        }

        private static Task<T[]> GenerateLoader<T>(RememberingParquetReader reader, DataField column)
        {
            return new Task<T[]>(() => LoadColumn<T>(reader, column).Result);
        }

        public bool IsSaved { get; }
        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            if (IsSaved)
            {
                using (var stream = await _archive.OpenFile(_zipEntry))
                {
                    writer.StoreFile(stream, _zipEntry.Name);
                }

                return true;
            }

            var tableName = "data.parquet";
            string tablePath;

            using (var ms = new MemoryStream())
            {
                using (var tableWriter = new ParquetWriter(_reader.Schema, ms))
                {
                    tableWriter.Write(_reader.ReadAsTable());
                }

                ms.Position = 0;
                tablePath = writer.StoreFile(ms, tableName);
            }

            if (!root.TryGetValue("user", out var user))
            {
                user = new JObject();
                root["user"] = user;
            }

            if (!root.TryGetValue("meta", out var meta))
            {
                meta = new JObject();
                root["meta"] = meta;
            }
            root["meta"]["type"] = Type;
            root["meta"]["path"] = tablePath;

            return true;
        }
    }

    [ArchivePlugin("no.sintef.table")]
    public class ArchiveTablePlugin : IArchivePlugin
    {
        private async Task<ArchiveTableInformation> ParseTableInformation(JObject json, Archive.Archive archive)
        {
            // Find the properties in the JSON
            ArchiveStructure.GetUserMeta(json, out var meta, out var user);
            var path = meta["path"].ToObject<string>() ?? throw new ArgumentException("Table is missing 'path'");

            // Find the file in the archive
            var zipEntry = archive.FindFile(path) ?? throw new ZipException($"Table file '{path}' not found in archive");

            var tableInformation = new ArchiveTableInformation
            {
                ZipEntry = zipEntry,
                Columns = new List<DataField>(),
            };

            // Open the table file
            using (var stream = await archive.OpenFile(zipEntry))
            using (var reader = new ParquetReader(stream))
            {
                var fields = reader.Schema.GetDataFields();

                // Find all the column information
                foreach (var field in fields)
                {
                    if (field.Name.Equals("time", StringComparison.OrdinalIgnoreCase))
                    {
                        tableInformation.Time = field;
                    }
                    else
                    {
                        tableInformation.Columns.Add(field);
                    }
                }
            }

            // Return the collected info
            return tableInformation;
        }

        public async Task<ArchiveStructure> CreateFromJSON(JObject json, Archive.Archive archive)
        {
            var information = await ParseTableInformation(json, archive);
            return new ArchiveTable(json, archive, information);
        }
    }

    /* -- Helper structs -- */
    public struct ArchiveTableInformation
    {
        public ZipEntry ZipEntry;
        public DataField Time;
        public List<DataField> Columns;
    }
}
