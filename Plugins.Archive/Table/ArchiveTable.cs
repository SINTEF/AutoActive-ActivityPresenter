using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using ICSharpCode.SharpZipLib.Zip;

using SINTEF.AutoActive.Archive;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Table;
using System.Threading.Tasks;
using Parquet;
using Parquet.Data;
using System.Threading;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;

namespace SINTEF.AutoActive.Plugins.ArchivePlugins.Table
{
    public class ArchiveTable : ArchiveStructure
    {
        public override string Type => "no.sintef.table";

        internal ArchiveTable(JObject json, Archive.Archive archive, ArchiveTableInformation tableInformation) : base(json)
        {
            var zipEntry = tableInformation.zipEntry;
            // TODO: Multiple indices?
            if (tableInformation.time == null) throw new ArgumentException("Table does not have a column named 'Time'");
            var timeInfo = tableInformation.time;
            var time = new TableIndex(timeInfo.Name, GenerateLoader<double>(archive, zipEntry, timeInfo));

            // Add all the other columns
            foreach (var column in tableInformation.columns)
            {
                switch (column.DataType)
                {
                    case DataType.Boolean:
                        this.AddColumn(column.Name, GenerateLoader<bool>(archive, zipEntry, column), time);
                        break;

                    case DataType.Byte:
                        this.AddColumn(column.Name, GenerateLoader<byte>(archive, zipEntry, column), time);
                        break;
                    case DataType.Int32:
                        this.AddColumn(column.Name, GenerateLoader<int>(archive, zipEntry, column), time);
                        break;
                    case DataType.Int64:
                        this.AddColumn(column.Name, GenerateLoader<long>(archive, zipEntry, column), time);
                        break;

                    case DataType.Float:
                        this.AddColumn(column.Name, GenerateLoader<float>(archive, zipEntry, column), time);
                        break;
                    case DataType.Double:
                        this.AddColumn(column.Name, GenerateLoader<double>(archive, zipEntry, column), time);
                        break;

                    default:
                        throw new InvalidOperationException($"Cannot read {column.DataType} columns");
                }
            }
        }

        /*
        protected override void RegisterContents(DataStructureAddedToHandler dataStructureAdded, DataPointAddedToHandler dataPointAdded)
        {
            foreach (var column in columns)
            {
                dataPointAdded?.Invoke(column, this);
            }
        }

        protected override void ToArchiveJSON(JObject meta, JObject user)
        {
            throw new NotImplementedException();
        }
        */

        /* -- Loader generation -- */
        private static async Task<T[]> LoadColumn<T>(Archive.Archive archive, ZipEntry zipEntry, DataField column)
        {
            // Open the table file
            using (var stream = await archive.OpenFile(zipEntry))
            using (var reader = new ParquetReader(stream))
            {
                // Find the datafield we want to use
                var datafield = Array.Find(reader.Schema.GetDataFields(), (field) => field.Name == column.Name);
                if (datafield == null) throw new ArgumentException($"Couldn't find column {column.Name} in table");

                T[] data = null;

                // Read the data pages
                for (int page = 0; page < reader.RowGroupCount; page++)
                {
                    // TODO: Do this asynchronously?
                    var pageReader = reader.OpenRowGroupReader(page);
                    var dataColumn = pageReader.ReadColumn(datafield);
                    var prevLength = data?.Length ?? 0;
                    Array.Resize(ref data, prevLength + dataColumn.Data.Length);
                    Array.Copy(dataColumn.Data, 0, data, prevLength, dataColumn.Data.Length);
                }

                return data;
            }
        }
 
        private static Task<T[]> GenerateLoader<T>(Archive.Archive archive, ZipEntry zipEntry, DataField column)
        {
            return new Task<T[]>(() => LoadColumn<T>(archive, zipEntry, column).Result);
        }
    }
    /*
    public class ArchiveTableColumn : IDataPoint
    {
        ZipEntry zipEntry;
        Archive.Archive archive;
        bool loaded;
        ArchiveTableIndex index;
        protected float[] data;
        SemaphoreSlim locker = new SemaphoreSlim(1, 1);

        internal ArchiveTableColumn(string name, ZipEntry zipEntry, Archive.Archive archive, ArchiveTableIndex index)
        {
            Name = name;
            this.zipEntry = zipEntry;
            this.archive = archive;
            this.index = index;
            loaded = false;
        }

        public Span<float> GetData(int start, int end)
        {
            return data.AsSpan(start, end - start);
        }

        public Type Type => throw new NotImplementedException();

        public string Name { get; set; }

        protected async Task EnsureLoaded()
        {
            await locker.WaitAsync();
            try
            {
                if (!loaded)
                {
                    // Open the table file
                    using (var stream = await archive.OpenFile(zipEntry))
                    using (var reader = new ParquetReader(stream))
                    {
                        // Find the correct column name
                        DataField dataField = null;
                        foreach (var field in reader.Schema.GetDataFields())
                        {
                            if (field.Name == Name)
                            {
                                switch (field.DataType)
                                {
                                    case DataType.Int32:
                                    case DataType.Int64:
                                    case DataType.Float:
                                    case DataType.Double:
                                        dataField = field;
                                        break;
                                    default:
                                        throw new InvalidOperationException($"Cannot read {field.DataType} columns");
                                }
                                break;
                            }
                        }

                        if (dataField == null) throw new ArgumentException($"Couldn't find column {Name} in table");

                        // Read the data pages
                        for (int page = 0; page < reader.RowGroupCount; page++)
                        {
                            // TODO: Do this asynchronously?
                            var pageReader = reader.OpenRowGroupReader(page);
                            var column = pageReader.ReadColumn(dataField);
                            var prevLength = data?.Length ?? 0;
                            Array.Resize(ref data, prevLength + column.Data.Length);
                            switch (column.Data)
                            {
                                case Int32[] ints:
                                    foreach (var num in ints)
                                    {
                                        data[prevLength] = num;
                                        prevLength++;
                                    }
                                    break;
                                case Int64[] ints:
                                    foreach (var num in ints)
                                    {
                                        data[prevLength] = num;
                                        prevLength++;
                                    }
                                    break;
                                case Single[] floats:
                                    foreach (var num in floats)
                                    {
                                        data[prevLength] = num;
                                        prevLength++;
                                    }
                                    break;
                                case Double[] doubles:
                                    foreach (var num in doubles)
                                    {
                                        data[prevLength] = (float)num;
                                        prevLength++;
                                    }
                                    break;
                            }
                        }
                    }
                    loaded = true;
                }
            }
            finally
            {
                locker.Release();
            }
        }

        public async Task<IDataViewer> CreateViewerIn(DataViewerContext context)
        {
            var l1 = EnsureLoaded();
            var l2 = index.EnsureLoaded();
            await l1;
            await l2;
            return new ArchiveTableColumnViewer(index, this, context);
        }
    }

    public class ArchiveTableIndex : ArchiveTableColumn
    {
        internal ArchiveTableIndex(string name, ZipEntry zipEntry, Archive.Archive archive)
            : base(name, zipEntry, archive, null)
        {

        }

        internal int findIndex(int current, double value)
        {
            // FIXME: This is far from perfect
            if (current >= 0 && data[current] == value) return current;

            // Do a binary search starting at the previous index
            int first = 0;
            int last = data.Length - 1;

            if (current < 0) current = (first + last) / 2;

            while (first < last)
            {
                if (value < data[first]) return first;
                if (value > data[last]) return last;

                if (value > data[current]) first = current+1;
                else last = current-1;
                current = (last + first) / 2;

            }
            return current;
        }
    }

    public class ArchiveTableColumnViewer : IDataViewer
    {
        ArchiveTableIndex time;
        ArchiveTableColumn column;
        DataViewerContext context;
        int startIndex = -1;
        int endIndex = -1;

        internal ArchiveTableColumnViewer(ArchiveTableIndex time, ArchiveTableColumn column, DataViewerContext context)
        {
            this.time = time;
            this.column = column;
            this.context = context;
            context.RangeUpdated += Context_RangeUpdated;
            Context_RangeUpdated(context.RangeFrom, context.RangeTo);
        }

        private void Context_RangeUpdated(double from, double to)
        {
            var start = time.findIndex(startIndex, from);
            var end = time.findIndex(endIndex, to);
            if (start != startIndex || end != endIndex)
            {
                startIndex = start;
                endIndex = end;
                Changed?.Invoke();
            }
        }

        public IDataPoint DataPoint { get; }

        public event DataViewWasChangedHandler Changed;

        public SpanPair<float> GetCurrentFloat()
        {
            return new SpanPair<float>(time.GetData(startIndex, endIndex), column.GetData(startIndex, endIndex));
        }

        public Span<byte> GetCurrentData()
        {
            throw new NotImplementedException();
        }
    }
    */
    [ArchivePlugin("no.sintef.table")]
    public class ArchiveTablePlugin : IArchivePlugin
    {
        private async Task<ArchiveTableInformation> ParseTableInformation(JObject json, Archive.Archive archive)
        {
            // Find the properties in the JSON
            ArchiveStructure.GetUserMeta(json, out var meta, out var user);
            var path = meta["path"].ToObject<string>() ?? throw new ArgumentException("Table is missing 'path'");
            var columns = meta["columns"].ToObject<string[]>() ?? throw new ArgumentException("Table is missing 'columns'");
            var index = meta["index"].ToObject<int[]>() ?? throw new ArgumentException("Table is missing 'index'");

            // Check the metadata
            if (columns.Length != index.Length) throw new ArgumentException("'columns' and 'index' are not the same length");

            // Find the file in the archive
            var zipEntry = archive.FindFile(path) ?? throw new ZipException($"Table file '{path}' not found in archive");

            var tableInformation = new ArchiveTableInformation
            {
                zipEntry = zipEntry,
                columns = new List<DataField>(),
            };

            // Open the table file
            using (var stream = await archive.OpenFile(zipEntry))
            using (var reader = new ParquetReader(stream))
            {
                var fields = reader.Schema.GetDataFields();

                // Find all the column information
                foreach (var column in columns)
                {
                    foreach (var field in fields)
                    {
                        if (field.Name == column)
                        {
                            if (column.Equals("time", StringComparison.OrdinalIgnoreCase))
                            {
                                tableInformation.time = field;
                            }
                            else
                            {
                                tableInformation.columns.Add(field);
                            }
                        }
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
    internal struct ArchiveTableInformation
    {
        public ZipEntry zipEntry;
        public DataField time;
        public List<DataField> columns;
    }
}
