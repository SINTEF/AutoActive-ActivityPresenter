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
    public class ArchiveTable : ArchiveStructure, ISaveable
    {
        public override string Type => "no.sintef.table";
        private readonly ZipEntry _zipEntry;
        private readonly ParquetReader _reader;
        private readonly Archive.Archive _archive;

        internal ArchiveTable(JObject json, Archive.Archive archive, ArchiveTableInformation tableInformation) : base(json)
        {
            IsSaved = true;
            _archive = archive;
            _zipEntry = tableInformation.ZipEntry;
            if (tableInformation.Time == null) throw new ArgumentException("Table does not have a column named 'Time'");
            var timeInfo = tableInformation.Time;
            var time = new TableTimeIndex(timeInfo.Name, GenerateLoader<long>(archive, _zipEntry, timeInfo), false);
            // FIXME: Implement synchronization metadata

            // Add all the other columns
            foreach (var column in tableInformation.Columns)
            {
                switch (column.DataType)
                {
                    case DataType.Boolean:
                        this.AddColumn(column.Name, GenerateLoader<bool>(archive, _zipEntry, column), time);
                        break;

                    case DataType.Byte:
                        this.AddColumn(column.Name, GenerateLoader<byte>(archive, _zipEntry, column), time);
                        break;
                    case DataType.Int32:
                        this.AddColumn(column.Name, GenerateLoader<int>(archive, _zipEntry, column), time);
                        break;
                    case DataType.Int64:
                        this.AddColumn(column.Name, GenerateLoader<long>(archive, _zipEntry, column), time);
                        break;

                    case DataType.Float:
                        this.AddColumn(column.Name, GenerateLoader<float>(archive, _zipEntry, column), time);
                        break;
                    case DataType.Double:
                        this.AddColumn(column.Name, GenerateLoader<double>(archive, _zipEntry, column), time);
                        break;

                    default:
                        throw new InvalidOperationException($"Cannot read {column.DataType} columns");
                }
            }
        }

        public ArchiveTable(JObject json, ParquetReader reader, ArchiveTableInformation tableInformation, string name) :
            base(json)
        {
            Name = name;
            _reader = reader;
            IsSaved = false;
            var timeInfo = tableInformation.Time;
            var time = new TableTimeIndex(timeInfo.Name, GenerateLoader<long>(reader, timeInfo), false);

            foreach (var column in tableInformation.Columns)
            {
                switch (column.DataType)
                {
                    case DataType.Boolean:
                        this.AddColumn(column.Name, GenerateLoader<bool>(reader, column), time);
                        break;

                    case DataType.Byte:
                        this.AddColumn(column.Name, GenerateLoader<byte>(reader, column), time);
                        break;
                    case DataType.Int32:
                        this.AddColumn(column.Name, GenerateLoader<int>(reader, column), time);
                        break;
                    case DataType.Int64:
                        this.AddColumn(column.Name, GenerateLoader<long>(reader, column), time);
                        break;

                    case DataType.Float:
                        this.AddColumn(column.Name, GenerateLoader<float>(reader, column), time);
                        break;
                    case DataType.Double:
                        this.AddColumn(column.Name, GenerateLoader<double>(reader, column), time);
                        break;

                    default:
                        throw new InvalidOperationException($"Cannot read {column.DataType} columns");
                }
            }
        }
        private static T[] DoLoadColumn<T>(ParquetReader reader, DataField column)
        {
            // Find the datafield we want to use
            var dataField = Array.Find(reader.Schema.GetDataFields(), field => field.Name == column.Name);
            if (dataField == null) throw new ArgumentException($"Couldn't find column {column.Name} in table");

            T[] data = null;

            // Read the data pages
            for (var page = 0; page < reader.RowGroupCount; page++)
            {
                // TODO: Do this asynchronously?
                var pageReader = reader.OpenRowGroupReader(page);
                var dataColumn = pageReader.ReadColumn(dataField);
                var prevLength = data?.Length ?? 0;
                Array.Resize(ref data, prevLength + dataColumn.Data.Length);
                Array.Copy(dataColumn.Data, 0, data, prevLength, dataColumn.Data.Length);
            }

            return data;
        }

        private static async Task<T[]> LoadColumn<T>(ParquetReader reader, DataField column)
        {
            return DoLoadColumn<T>(reader, column);
        }

        /* -- Loader generation -- */
        private static async Task<T[]> LoadColumn<T>(Archive.Archive archive, ZipEntry zipEntry, DataField column)
        {
            // Open the table file
            using (var stream = await archive.OpenFile(zipEntry))
            using (var reader = new ParquetReader(stream))
            {
                return DoLoadColumn<T>(reader, column);
            }
        }
 
        private static Task<T[]> GenerateLoader<T>(Archive.Archive archive, ZipEntry zipEntry, DataField column)
        {
            return new Task<T[]>(() => LoadColumn<T>(archive, zipEntry, column).Result);
        }

        private static Task<T[]> GenerateLoader<T>(ParquetReader reader, DataField column)
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

            using (var ms = new MemoryStream())
            {
                using (var tableWriter = new ParquetWriter(_reader.Schema, ms))
                {
                    tableWriter.Write(_reader.ReadAsTable());
                }

                ms.Position = 0;
                writer.StoreFile(ms, "data.parquet");
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
            root["meta"]["path"] = $"{writer.RootName}/data.parquet";

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
