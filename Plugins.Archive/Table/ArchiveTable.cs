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
using System.Linq;
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

        public RememberingParquetReader(RememberingParquetReader rpr)
        {
            // Make a copy of existing data and reader
            _reader = rpr._reader;
            _data = new Dictionary<DataField, Array>(rpr._data);
        }

        public Schema Schema => _reader.Schema;

        private readonly Dictionary<DataField, Array> _data = new Dictionary<DataField, Array>();

        public void LoadAll()
        {
            foreach (var column in _reader.Schema.GetDataFields())
            {
                if (_data.TryGetValue(column, out var arr))
                {
                    continue;
                }

                var t = DataType2Type(column.DataType);
                GetType().GetMethod("LoadColumn")?.MakeGenericMethod(t).Invoke(this, new object[] {column});
            }
        }

        public T[] LoadColumn<T>(DataField column)
        {
            if (_data.TryGetValue(column, out var arr))
            {
                return arr as T[];
            }

            //TODO: these should not be needed
            // Find the datafield we want to use
            var dataField = Array.Find(_reader.Schema.GetDataFields(), field => field.Name == column.Name);
            if (dataField == null) throw new ArgumentException($"Couldn't find column {column.Name} in table");

            T[] data = null;
            try
            {
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
            }
            catch (ArrayTypeMismatchException ex)
            {
                throw new ArrayTypeMismatchException($"Could not load column {column.Name}. The expected data is {typeof(T)} but actual data was {dataField.DataType}.\n\n{ex.Message}");
            }

            _data[column] = data;

            return data;
        }

        public static Type DataType2Type(DataType type)
        {
            switch (type)
            {
                case DataType.Boolean:
                    return typeof(bool);
                case DataType.Byte:
                    return typeof(byte);
                case DataType.Int32:
                    return typeof(int);
                case DataType.Int64:
                    return typeof(long);
                case DataType.Float:
                    return typeof(float);
                case DataType.Double:
                    return typeof(double);
                case DataType.SignedByte:
                    return typeof(sbyte);
                case DataType.UnsignedByte:
                    return typeof(byte);
                case DataType.Short:
                    return typeof(short);
                case DataType.UnsignedShort:
                    return typeof(ushort);
                case DataType.Int16:
                    return typeof(short);
                case DataType.UnsignedInt16:
                    return typeof(ushort);
                case DataType.Int96:
                    break;
                case DataType.ByteArray:
                    break;
                case DataType.String:
                    break;
                case DataType.Decimal:
                    break;
                case DataType.DateTimeOffset:
                    break;
                case DataType.Interval:
                    break;
                case DataType.Unspecified:
                    break;
            }

            throw new NotImplementedException($"Data type {type} not implemented.");
        }

        public Array GetColumn(DataField field)
        {
            return _data[field];
        }
    }

    public class ArchiveTable : ArchiveStructure, ISaveable
    {
        public override string Type => "no.sintef.table";
        private readonly ZipEntry _zipEntry;
        private readonly RememberingParquetReader _reader;
        private readonly Archive.Archive _archive;
        private readonly Guid _sessionId;

        internal ArchiveTable(JObject json, Archive.Archive archive, Guid sessionId, ArchiveTableInformation tableInformation) :
            base(json)
        {
            IsSaved = true;
            _archive = archive;
            _zipEntry = tableInformation.ZipEntry;
            _sessionId = sessionId;

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
            var tableFile = _sessionId + "://" + tableInformation.Uri;
            var uri2 = tableFile + "/" + timeInfo.Name;
            var time = new TableTimeIndex(timeInfo.Name, GenerateLoader<long>(_reader, timeInfo), tableInformation.IsWorldSynchronized, uri2, tableInformation.TimeUnit);

            for (var index = 0; index < tableInformation.Columns.Count; index++)
            {
                var column = tableInformation.Columns[index];
                string unit = null;
                if (index < tableInformation.Units?.Count)
                {
                    unit = tableInformation.Units[index];
                }
                if (column.HasNulls)
                {
                    throw new NotImplementedException("Nullable columns are not yet implemented.");
                }

                var uri = tableFile + "/" + column.Name;

                switch (column.DataType)
                {
                    case DataType.Boolean:
                        this.AddColumn(column.Name, GenerateLoader<bool>(_reader, column), time, uri, unit);
                        break;
                    case DataType.Byte:
                        this.AddColumn(column.Name, GenerateLoader<byte>(_reader, column), time, uri, unit);
                        break;
                    case DataType.Int32:
                        this.AddColumn(column.Name, GenerateLoader<int>(_reader, column), time, uri, unit);
                        break;
                    case DataType.Int64:
                        this.AddColumn(column.Name, GenerateLoader<long>(_reader, column), time, uri, unit);
                        break;
                    case DataType.Float:
                        this.AddColumn(column.Name, GenerateLoader<float>(_reader, column), time, uri, unit);
                        break;
                    case DataType.Double:
                        this.AddColumn(column.Name, GenerateLoader<double>(_reader, column), time, uri, unit);
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

        public bool IsSaved { get; set; }
        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            var pathArr = Meta["attachments"].ToObject<string[]>() ?? throw new ArgumentException("Table is missing 'attachments'");

            //TODO: Implement?
            if (false && IsSaved)
            {
                var stream = await _archive.OpenFile(_zipEntry);

                writer.StoreFileId(stream, pathArr[0]);
            }
            else
            {
                // This stream will be disposed by the sessionWriter
                var ms = new MemoryStream();

                // Make a copy of the Remembering reader that later can be discarded
                // This to avoid to read in all tables in memory at the same time.
                var fullReader = new RememberingParquetReader(_reader);
                fullReader.LoadAll();
                using (var tableWriter = new ParquetWriter(fullReader.Schema, ms))
                {
                    using (var rowGroup = tableWriter.CreateRowGroup())  // Using construction assure correct storage of final rowGroup details in parquet file
                    {
                        foreach (var field in fullReader.Schema.GetDataFields())
                        {
                            var column = new DataColumn(field, fullReader.GetColumn(field));
                            rowGroup.WriteColumn(column);
                        }
                    }
                }

                ms.Position = 0;
                writer.StoreFileId(ms, pathArr[0]);

            }

            // TODO AUTOACTIVE-58 - Generalize copy of previous metadata for save

            // Copy previous
            root["meta"] = Meta;
            root["user"] = User;

            // Overwrite potentially changed
            // TODO root["meta"]["is_world_clock"] = ;
            // TODO root["meta"]["synced_to"] =  ;

            return true;
        }
    }

    [ArchivePlugin("no.sintef.table")]
    public class ArchiveTablePlugin : IArchivePlugin
    {
        private async Task<ArchiveTableInformation> ParseTableInformation(JObject json, Archive.Archive archive, Guid sessionId)
        {
            // Find the properties in the JSON
            ArchiveStructure.GetUserMeta(json, out var meta, out var user);
            var pathArr = meta["attachments"].ToObject<string[]>() ?? throw new ArgumentException("Table is missing 'attachments'");
            var path = "" + sessionId + pathArr[0];

            // Find the file in the archive
            var zipEntry = archive.FindFile(path) ?? throw new ZipException($"Table file '{path}' not found in archive");

            var tableInformation = new ArchiveTableInformation
            {
                ZipEntry = zipEntry,
                Columns = new List<DataField>(),
                Uri = path,
                Units = new List<string>(),
                TimeUnit = "",
            };

            // Open the table file
            using (var stream = await archive.OpenFile(zipEntry))
            using (var reader = new ParquetReader(stream))
            {
                var fields = reader.Schema.GetDataFields();
                var rawUnits = new string[0];
                if (meta.ContainsKey("units"))
                {
                    rawUnits = meta["units"].Select(unit => unit.Value<string>()).ToArray();
                }

                // Find all the column information
                int idx = 0;
                foreach (var field in fields)
                {
                    // Fetch unit for current column
                    string currUnit = "";
                    if(idx < rawUnits.Length)
                    {
                        currUnit = rawUnits[idx];
                    }
                    idx++;

                    if (field.Name.Equals("time", StringComparison.OrdinalIgnoreCase))
                    {
                        tableInformation.Time = field;
                        tableInformation.TimeUnit = currUnit;
                    }
                    else
                    {
                        tableInformation.Columns.Add(field);
                        tableInformation.Units.Add(currUnit);
                    }
                }
            }

            if (meta.ContainsKey("is_world_clock"))
                tableInformation.IsWorldSynchronized = meta["is_world_clock"].Value<bool>();

            // Return the collected info
            return tableInformation;
        }

        public async Task<ArchiveStructure> CreateFromJSON(JObject json, Archive.Archive archive, Guid sessionId)
        {
            var information = await ParseTableInformation(json, archive, sessionId);
            return new ArchiveTable(json, archive, sessionId, information);
        }
    }

    /* -- Helper structs -- */
    public struct ArchiveTableInformation
    {
        public ZipEntry ZipEntry;
        public DataField Time;
        public string TimeUnit;
        public string Uri;
        public List<DataField> Columns;
        public List<string> Units;
        public bool IsWorldSynchronized;
    }
}
