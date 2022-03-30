using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public abstract class TableColumn : IDataPoint
    {
        protected TableTimeIndex Index;

        private readonly Task _loader;
        private readonly Mutex _loaderMutex = new Mutex();

        public event EventHandler DataChanged;
        public string URI { get; }
        public Type DataType { get; }
        public string Name { get; set; }

        internal double? MinValueHint { get; private set; }
        internal double? MaxValueHint { get; private set; }

        public ITimePoint Time => Index;
        public string Unit { get; set; }

        internal TableColumn(Type type, string name, Task loader, TableTimeIndex index, string uri)
        {
            DataType = type;
            Name = name;
            Index = index;
            _loader = loader;
            URI = uri;
        }

        // FIXME: Thread safety of the loading functions!!
        private void EnsureSelfIsLoaded()
        {
            lock (_loaderMutex)
            {
                if (_loader.IsCompleted) return;

                // Make sure the loading is done
                _loader.RunSynchronously();
                // Get the actual implementation to check the loaded Data
                var dataLength = CheckLoaderResultLength();
                if (Index != null && Index.Data.Length != dataLength)
                    throw new Exception($"Column {Name} is not the same length as Index");
                // Find the min and max values
                var (min, max) = GetDataMinMax();
                MinValueHint = min;
                MaxValueHint = max;
            }
        }

        private void EnsureIndexAndDataIsLoaded()
        {
            if (_loader.IsCompleted) return;

            // Load the index Data
            Index.EnsureSelfIsLoaded();
            // Load our own Data
            EnsureSelfIsLoaded();
        }

        public Task<IDataViewer> CreateViewer()
        {
            EnsureIndexAndDataIsLoaded();
            switch (this)
            {
                case StringColumn _:
                    return Task.FromResult(CreateStringViewer(Index));
                default:
                    return Task.FromResult(CreateGenericViewer(Index));
            }
        }

        protected abstract int CheckLoaderResultLength();
        protected abstract (double? min, double? max) GetDataMinMax();

        protected virtual IDataViewer CreateStringViewer(TableTimeIndex index) { throw new NotSupportedException(); }
        protected virtual IDataViewer CreateGenericViewer(TableTimeIndex index) { throw new NotSupportedException(); }
    }

    public abstract class TableColumnViewer : ITimeSeriesViewer
    {
        protected TableTimeIndex Index;
        protected int StartIndex = -1;
        protected int EndIndex = -1;
        protected int Length = -1;

        public long PreviewPercentage { get; set; }

        protected TableColumnViewer(TableTimeIndex index, TableColumn column)
        {
            Index = index;
            Column = column;
        }

        public void SetTimeRange(long from, long to)
        {
            var diff = to - from;
            var startTime = from - diff * PreviewPercentage / 100;
            var endTime = from + diff;

            var start = Index.FindIndex(StartIndex, startTime);
            var end = Index.FindIndex(EndIndex, endTime);
            CurrentTimeRangeFrom = from;
            CurrentTimeRangeTo = to;

            if (start == StartIndex && end == EndIndex) return;

            StartIndex = start;
            EndIndex = end;
            Length = EndIndex - StartIndex + 1;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public TableColumn Column { get; private set; }
        public IDataPoint DataPoint => Column;

        public event EventHandler Changed;

        public double? MinValueHint => Column.MinValueHint;
        public double? MaxValueHint => Column.MaxValueHint;

        public long CurrentTimeRangeFrom { get; private set; }
        public long CurrentTimeRangeTo { get; private set; }

        public virtual SpanPair<bool> GetCurrentBools() { throw new NotSupportedException(); }
        public virtual SpanPair<string> GetCurrentStrings() { throw new NotSupportedException(); }
        public virtual SpanPair<T> GetCurrentData<T>() where T : IConvertible { throw new NotSupportedException(); }
    }
}
