using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Plugins.Import.Mqtt.Columns;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Plugins.Import.Mqtt
{
    public class TableTimeIndexDyn : LongColumnDyn, ITimePoint
    {

        public TableTimeIndexDyn(string name, bool isWorldClockSynchronized) : base(name, null)
        {
            IsSynchronizedToWorldClock = isWorldClockSynchronized;
        }

        internal int FindIndex(int current, long value)
        {
            // FIXME: This is far from perfect
            if (current >= 0 && data[current] == value) return current;
            // Do a binary search starting at the previous index
            int first = 0;
            int last = length - 1;
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
            // await CreateViewer();
            var tv = new TableTimeIndexDynViewer(this);
            dynDataViewers.Add(tv);
            return tv;
        }

        // public Task<ITimeViewer> CreateViewer()
        // {
        //    throw new NotImplementedException();
        // }


        public bool IsSynchronizedToWorldClock { get; private set; }

        //internal override long HasDataFrom => data[0];
        //internal override long HasDataTo => data[length - 1];
    }

    public class TableTimeIndexDynViewer : LongColumnDynViewer, ITimeViewer, IDynDataViewer
    {
        private TableTimeIndexDyn time;
        internal TableTimeIndexDynViewer(TableTimeIndexDyn index) : base(null, index)
        {
            time = index;
        }
        public ITimePoint TimePoint => time;
        public long Start => time.data[0];
        public long End => time.data[time.length - 1];
        // Will never happen, so no point in implementing it
        event TimeViewerWasChangedHandler ITimeViewer.Changed { add { } remove { } }

        void IDynDataViewer.DataUpdated()
        {
            // TODO
        }

    }
}
