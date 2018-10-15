using SINTEF.AutoActive.Databus.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Plugins.Import.Mqtt.Columns
{
    public class LongColumnDyn : TableColumnDyn
    {
        internal long[] data;
        internal int length = 0;

        public LongColumnDyn(string name, TableTimeIndexDyn index) : base(typeof(long), name, index)
        {
            data = new long[100];
            length = 0;
        }

        public void AddData(long val)
        {
            if (length == 0)
            {
                MinValueHint = val;
                MaxValueHint = val;
            }
            else
            {
                if (val < MinValueHint) MinValueHint = val;
                if (val > MaxValueHint) MaxValueHint = val;
            }

            if (length >= data.Length)
                Array.Resize(ref data, data.Length * 2);

            data[length] = val;
            length++;

            if (index != null && index.length != length) throw new Exception($"Column {Name} is not the same length as Index");
        }

        protected override TableColumnDynViewer CreateDoubleDynViewer(TableTimeIndexDyn index)
        {
            return new LongColumnDynViewer(index, this);
        }
    }

    public class LongColumnDynViewer : TableColumnDynViewer
    {
        private LongColumnDyn column;

        internal LongColumnDynViewer(TableTimeIndexDyn index, LongColumnDyn column) : base(index, column)
        {
            this.column = column;
        }

        public override SpanPair<long> GetCurrentLongs()
        {
            return new SpanPair<long>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }

}
