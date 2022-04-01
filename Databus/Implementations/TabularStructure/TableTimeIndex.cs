using System.Collections.Generic;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.Interfaces;
using System.Threading.Tasks;
using System;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public class TableTimeIndex : GenericColumn<long>, ITimePoint
    {
        private readonly List<TableTimeIndexViewer> _viewers = new List<TableTimeIndexViewer>();
        private readonly long _initialStartTime;
        private bool _pendingApplyStartTime = false;

        public TableTimeIndex(string name, Task<long[]> loader, bool isWorldClockSynchronized, string uri, string unit) : base(name, loader, null, uri, unit)
        {
            IsSynchronizedToWorldClock = isWorldClockSynchronized;
            _pendingApplyStartTime = false;
        }

        public TableTimeIndex(string name, Task<long[]> loader, bool isWorldClockSynchronized, string uri, string unit, long startTime) : base(name, loader, null, uri, unit)
        {
            IsSynchronizedToWorldClock = isWorldClockSynchronized;
            _pendingApplyStartTime = true;
            _initialStartTime = startTime;
        }

        protected override int CheckLoaderResultLength()
        {
            var len = base.CheckLoaderResultLength();

            if (_pendingApplyStartTime)
            {
                _pendingApplyStartTime = false;
                TransformTime(_initialStartTime, 1d);
            }

            return len;
        }


        internal int FindClosestIndex(int current, long value)
        {
            var index = Array.BinarySearch(Data, value);

            // BinarySearch returns a 2's complement if the value was not found.
            if (index < 0) index = ~index;

            return index;
        }

        async Task<ITimeViewer> ITimePoint.CreateViewer()
        {
            // Ensure that the data is loaded
            await CreateViewer();
            var viewer = new TableTimeIndexViewer(this);
            _viewers.Add(viewer);
            return viewer;
        }

        public void TransformTime(long offset, double scale)
        {
            for (var i = 0; i < Data.Length; i++)
            {
                Data[i] = (long)(Data[i] * scale + offset);
            }

            foreach (var viewer in _viewers)
            {
                viewer.UpdatedTimeIndex();
            }
        }

        public long Start => Data[0];
        public long End => Data[Data.Length - 1];

        public bool IsSynchronizedToWorldClock { get; private set; }
    }

    public class TableTimeIndexViewer : ITimeViewer
    {
        private readonly TableTimeIndex _time;

        internal TableTimeIndexViewer(TableTimeIndex index)
        {
            _time = index;
        }

        public void UpdatedTimeIndex()
        {
            TimeChanged?.Invoke(this, Start, End);
        }

        public ITimePoint TimePoint => _time;

        public long Start => _time.Start;
        public long End => _time.End;

        public event TimeViewerWasChangedHandler TimeChanged;
    }
}
