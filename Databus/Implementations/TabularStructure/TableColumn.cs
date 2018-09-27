using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public abstract class TableColumn : IDataPoint
    {
        protected TableIndex index;

        private Task loader;

        public Type DataType { get; private set; }
        public string Name { get; set; }

        internal virtual double HasDataFrom => index.HasDataFrom;
        internal virtual double HasDataTo => index.HasDataTo;

        internal double? MinValueHint { get; private set; }
        internal double? MaxValueHint { get; private set; }

        internal TableColumn(Type type, string name, Task loader, TableIndex index)
        {
            DataType = type;
            Name = name;
            this.index = index;
            this.loader = loader;
        }

        // FIXME: Thread safety of the loading functions!!
        private async Task EnsureSelfIsLoaded()
        {
            if (!loader.IsCompleted)
            {
                // Make sure the loading is done
                loader.Start();
                await loader;
                // Get the actual implementation to check the loaded data
                var dataLength = CheckLoaderResultLength();
                if (index != null && index.data.Length != dataLength) throw new Exception($"Column {Name} is not the same length as Index");
                // Find the min and max values
                var (min, max) = GetDataMinMax();
                MinValueHint = min;
                MaxValueHint = max;
            }
        }

        private async Task EnsureIndexAndDataIsLoaded()
        {
            if (!loader.IsCompleted)
            {
                // Load the index data
                await index.EnsureSelfIsLoaded();
                // Load our own data
                await EnsureSelfIsLoaded();
            }
        }

        public async Task<IDataViewer> CreateViewerIn(DataViewerContext context)
        {
            switch (this)
            {
                case BoolColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateBoolViewer(index, context);
                case ByteColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateByteViewer(index, context);
                case IntColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateIntViewer(index, context);
                case LongColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateLongViewer(index, context);
                case FloatColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateFloatViewer(index, context);
                case DoubleColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateDoubleViewer(index, context);
                case StringColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateStringViewer(index, context);
                default:
                    throw new NotSupportedException();
            }
        }

        protected abstract int CheckLoaderResultLength();
        protected abstract (double? min, double? max) GetDataMinMax();

        protected virtual IDataViewer CreateBoolViewer(TableIndex index, DataViewerContext context) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateByteViewer(TableIndex index, DataViewerContext context) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateIntViewer(TableIndex index, DataViewerContext context) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateLongViewer(TableIndex index, DataViewerContext context) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateFloatViewer(TableIndex index, DataViewerContext context) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateDoubleViewer(TableIndex index, DataViewerContext context) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateStringViewer(TableIndex index, DataViewerContext context) { throw new NotSupportedException(); }
    }

    public abstract class TableColumnViewer : ITimeSeriesViewer
    {
        protected TableIndex index;
        protected int startIndex = -1;
        protected int endIndex = -1;
        protected int length = -1;

        protected TableColumnViewer(TableIndex index, TableColumn column, DataViewerContext context)
        {
            this.index = index;
            Column = column;
            context.RangeUpdated += RangeUpdated;
            RangeUpdated(context.RangeFrom, context.RangeTo);
        }

        private void RangeUpdated(double from, double to)
        {
            var start = index.FindIndex(startIndex, from);
            var end = index.FindIndex(endIndex, to);
            if (start != startIndex || end != endIndex)
            {
                startIndex = start;
                endIndex = end;
                length = endIndex - startIndex + 1;
                Changed?.Invoke();
            }
        }

        public TableColumn Column { get; private set; }
        public IDataPoint DataPoint => Column;

        public event DataViewWasChangedHandler Changed;

        public double HasDataFrom => Column.HasDataFrom;
        public double HasDataTo => Column.HasDataTo;

        public double? MinValueHint => Column.MinValueHint;
        public double? MaxValueHint => Column.MaxValueHint;

        public event DataViewHasDataRangeChangedHandler HasDataRangeChanged; // Will never happen

        public virtual SpanPair<bool> GetCurrentBools() { throw new NotSupportedException(); }
        public virtual SpanPair<byte> GetCurrentBytes() { throw new NotSupportedException(); }
        public virtual SpanPair<int> GetCurrentInts() { throw new NotSupportedException(); }
        public virtual SpanPair<long> GetCurrentLongs() { throw new NotSupportedException(); }
        public virtual SpanPair<float> GetCurrentFloats() { throw new NotSupportedException(); }
        public virtual SpanPair<double> GetCurrentDoubles() { throw new NotSupportedException(); }
        public virtual SpanPair<string> GetCurrentStrings() { throw new NotSupportedException(); }
    }
}
