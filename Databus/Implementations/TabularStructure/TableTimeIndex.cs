using System.Collections.Generic;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.Interfaces;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public class TableTimeIndex : GenericColumn<long>, ITimePoint
    {
        private readonly List<TableTimeIndexViewer> _viewers = new List<TableTimeIndexViewer>();
        public TableTimeIndex(string name, Task<long[]> loader, bool isWorldClockSynchronized, string uri, string unit) : base(name, loader, null, uri, unit)
        {
            IsSynchronizedToWorldClock = isWorldClockSynchronized;
        }

        internal int FindIndex(int current, long value)
        {
            // FIXME: This is far from perfect
           if (current >= 0 && Data[current] == value) return current;

            // Do a binary search starting at the previous index
            var first = 0;
            var last = Data.Length - 1;

            if (current < 0) current = (first + last) / 2;

            while (first < last)
            {
                if (value < Data[first]) return first;
                if (value > Data[last]) return last;

                if (value > Data[current]) first = current + 1;
                else last = current - 1;
                current = (last + first) / 2;

            }
            return current;
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

        public bool IsSynchronizedToWorldClock { get; private set; }

    }

    public class TableTimeIndexViewer : GenericColumnViewer<long>, ITimeViewer
    {
        private readonly TableTimeIndex _time;

        internal TableTimeIndexViewer(TableTimeIndex index) : base(null, index)
        {
            _time = index;
        }

        public void UpdatedTimeIndex()
        {
            TimeChanged?.Invoke(this, Start, End);
        }

        public ITimePoint TimePoint => _time;

        public long Start => _time.Data[0];
        public long End => _time.Data[_time.Data.Length - 1];

        public event TimeViewerWasChangedHandler TimeChanged;
    }
}
