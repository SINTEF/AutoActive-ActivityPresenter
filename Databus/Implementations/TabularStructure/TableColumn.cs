using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public abstract class TableColumn : IDataPoint
    {
        protected TableTimeIndex Index;

        private readonly Task _loader;

        public Type DataType { get; private set; }
        public string Name { get; set; }

        internal double? MinValueHint { get; private set; }
        internal double? MaxValueHint { get; private set; }

        public ITimePoint Time => Index;

        internal TableColumn(Type type, string name, Task loader, TableTimeIndex index)
        {
            DataType = type;
            Name = name;
            Index = index;
            _loader = loader;
        }

        // FIXME: Thread safety of the loading functions!!
        private async Task EnsureSelfIsLoaded()
        {
            if (!_loader.IsCompleted)
            {
                // Make sure the loading is done
                _loader.Start();
                await _loader;
                // Get the actual implementation to check the loaded Data
                var dataLength = CheckLoaderResultLength();
                if (Index != null && Index.Data.Length != dataLength) throw new Exception($"Column {Name} is not the same length as Index");
                // Find the min and max values
                var (min, max) = GetDataMinMax();
                MinValueHint = min;
                MaxValueHint = max;
            }
        }

        private async Task EnsureIndexAndDataIsLoaded()
        {
            if (!_loader.IsCompleted)
            {
                // Load the index Data
                await Index.EnsureSelfIsLoaded();
                // Load our own Data
                await EnsureSelfIsLoaded();
            }
        }

        public async Task<IDataViewer> CreateViewer()
        {
            switch (this)
            {
                case BoolColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateBoolViewer(Index);
                case ByteColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateByteViewer(Index);
                case IntColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateIntViewer(Index);
                case LongColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateLongViewer(Index);
                case FloatColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateFloatViewer(Index);
                case DoubleColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateDoubleViewer(Index);
                case StringColumn c:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateStringViewer(Index);
                default:
                    await EnsureIndexAndDataIsLoaded();
                    return CreateGenericViewer(Index);
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
        protected virtual IDataViewer CreateGenericViewer(TableTimeIndex index) { throw new NotSupportedException(); }
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
            var plotWindow = GetPlotWindow(from, to);
            var startTime = plotWindow.Item1;
            var endTime = plotWindow.Item2;

            var start = Index.FindIndex(StartIndex, startTime);
            var end = Index.FindIndex(EndIndex, endTime);
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

        public static Tuple<long,long> GetPlotWindow(long from, long to)
        {
            var diff = (to - from);
            var startTime = from - diff * 1 / 3;
            var endTime = startTime + diff;
            return new Tuple<long, long>(startTime, endTime);
        }

        public virtual SpanPair<bool> GetCurrentBools() { throw new NotSupportedException(); }
        public virtual SpanPair<byte> GetCurrentBytes() { throw new NotSupportedException(); }
        public virtual SpanPair<int> GetCurrentInts() { throw new NotSupportedException(); }
        public virtual SpanPair<long> GetCurrentLongs() { throw new NotSupportedException(); }
        public virtual SpanPair<float> GetCurrentFloats() { throw new NotSupportedException(); }
        public virtual SpanPair<double> GetCurrentDoubles() { throw new NotSupportedException(); }
        public virtual SpanPair<string> GetCurrentStrings() { throw new NotSupportedException(); }

        public virtual SpanPair<T> GetCurrentData<T>() where T : IConvertible { throw new NotSupportedException(); }
    }
}
