using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.Interfaces;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure
{
    public class TableTimeIndex : LongColumn, ITimePoint
    {
        public TableTimeIndex(string name, Task<long[]> loader, bool isWorldClockSynchronized) : base(name, loader, null)
        {
            IsSynchronizedToWorldClock = isWorldClockSynchronized;
        }

        internal int FindIndex(int current, long value)
        {
            // FIXME: This is far from perfect
            if (current >= 0 && data[current] == value) return current;

            // Do a binary search starting at the previous index
            var first = 0;
            var last = data.Length - 1;

            if (current < 0) current = (first + last) / 2;

            while (first < last)
            {
                if (value < data[first]) return first;
                if (value > data[last]) return last;

                if (value > data[current]) first = current + 1;
                else last = current - 1;
                current = (last + first) / 2;

            }
            return current;
        }

        async Task<ITimeViewer> ITimePoint.CreateViewer()
        {
            // Ensure that the data is loaded
            await CreateViewer();
            return new TableTimeIndexViewer(this);
        }

        public bool IsSynchronizedToWorldClock { get; private set; }

    }

    public class TableTimeIndexViewer : LongColumnViewer, ITimeViewer
    {
        private readonly TableTimeIndex _time;

        internal TableTimeIndexViewer(TableTimeIndex index) : base(null, index)
        {
            _time = index;
        }

        public void UpdatedTimeIndex() { }

        public ITimePoint TimePoint => _time;

        public long Start => _time.data[0];
        public long End => _time.data[_time.data.Length - 1];

        // Will never happen, so no point in implementing it
        event TimeViewerWasChangedHandler ITimeViewer.TimeChanged { add { } remove { } }
    }
}
