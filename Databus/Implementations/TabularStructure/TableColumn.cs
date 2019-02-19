using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public abstract class TableColumn : IDataPoint
    {
        protected TableTimeIndex index;

        private Task loader;

        public Type DataType { get; private set; }
        public string Name { get; set; }

        internal double? MinValueHint { get; private set; }
        internal double? MaxValueHint { get; private set; }

        public ITimePoint Time => index;

        internal TableColumn(Type type, string name, Task loader, TableTimeIndex index)
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
                if (index != null && index.Data.Length != dataLength) throw new Exception($"Column {Name} is not the same length as Index");
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

        public async Task<IDataViewer> CreateViewer()
        {
            switch (this)
            {
                case BoolColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateBoolViewer(index);
                case ByteColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateByteViewer(index);
                case IntColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateIntViewer(index);
                case LongColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateLongViewer(index);
                case FloatColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateFloatViewer(index);
                case DoubleColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateDoubleViewer(index);
                case StringColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateStringViewer(index);
                default:
                    throw new NotSupportedException();
            }
        }

        protected abstract int CheckLoaderResultLength();
        protected abstract (double? min, double? max) GetDataMinMax();

        protected virtual IDataViewer CreateBoolViewer(TableTimeIndex index) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateByteViewer(TableTimeIndex index) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateIntViewer(TableTimeIndex index) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateLongViewer(TableTimeIndex index) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateFloatViewer(TableTimeIndex index) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateDoubleViewer(TableTimeIndex index) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateStringViewer(TableTimeIndex index) { throw new NotSupportedException(); }
    }

    public abstract class TableColumnViewer : ITimeSeriesViewer
    {
        protected TableTimeIndex Index;
        protected int StartIndex = -1;
        protected int EndIndex = -1;
        protected int Length = -1;

        protected TableColumnViewer(TableTimeIndex index, TableColumn column)
        {
            Index = index;
            Column = column;
        }

        public void SetTimeRange(long from, long to)
        {
            var start = Index.FindIndex(StartIndex, from);
            var end = Index.FindIndex(EndIndex, to);
            CurrentTimeRangeFrom = from;
            CurrentTimeRangeTo = to;
            if (start != StartIndex || end != EndIndex)
            {
                StartIndex = start;
                EndIndex = end;
                Length = EndIndex - StartIndex + 1;
                Changed?.Invoke(this);
            }
        }

        public TableColumn Column { get; private set; }
        public IDataPoint DataPoint => Column;

        public event DataViewerWasChangedHandler Changed;

        public double? MinValueHint => Column.MinValueHint;
        public double? MaxValueHint => Column.MaxValueHint;

        public long CurrentTimeRangeFrom { get; private set; }
        public long CurrentTimeRangeTo { get; private set; }

        public virtual SpanPair<bool> GetCurrentBools() { throw new NotSupportedException(); }
        public virtual SpanPair<byte> GetCurrentBytes() { throw new NotSupportedException(); }
        public virtual SpanPair<int> GetCurrentInts() { throw new NotSupportedException(); }
        public virtual SpanPair<long> GetCurrentLongs() { throw new NotSupportedException(); }
        public virtual SpanPair<float> GetCurrentFloats() { throw new NotSupportedException(); }
        public virtual SpanPair<double> GetCurrentDoubles() { throw new NotSupportedException(); }
        public virtual SpanPair<string> GetCurrentStrings() { throw new NotSupportedException(); }

    }
}
