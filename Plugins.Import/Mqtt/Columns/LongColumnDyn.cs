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

        internal long MinValueHintLong { get; set; }
        internal long MaxValueHintLong { get; set; }

        public LongColumnDyn(string name, TableTimeIndexDyn index) : base(typeof(long), name, index)
        {
            data = new long[100];
            length = 0;
        }

        public void AddData(long val)
        {
            if (length == 0)
            {
                MinValueHintLong = val;
                MinValueHint = val;
                MaxValueHintLong = val;
                MaxValueHint = val;
            }
            else
            {
                if (val < MinValueHintLong)
                {
                    MinValueHintLong = val;
                    MinValueHint = val;
                }
                if (val > MaxValueHintLong)
                {
                    MaxValueHintLong = val;
                    MaxValueHint = val;
                }
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

    //TODO: This class is likely deprecated
    public class LongColumnDynViewer : TableColumnDynViewer
    {
        private LongColumnDyn column;

        internal LongColumnDynViewer(TableTimeIndexDyn index, LongColumnDyn column) : base(index, column)
        {
            this.column = column;
        }

        public SpanPair<long> GetCurrentLongs()
        {
            return new SpanPair<long>(startIndex, index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }

}
