using SINTEF.AutoActive.Databus.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Plugins.Import.Mqtt.Columns
{
    public class DoubleColumnDyn : TableColumnDyn
    {
        internal double[] data;
        internal int length = 0;

        public DoubleColumnDyn(string name, TableTimeIndexDyn index) : base(typeof(double), name, index)
        {
            data = new double[100];
            length = 0;
        }

        public void AddData(double val)
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
            return new DoubleColumnDynViewer(index, this);
        }
    }

    public class DoubleColumnDynViewer : TableColumnDynViewer
    {
        private DoubleColumnDyn column;

        internal DoubleColumnDynViewer(TableTimeIndexDyn index, DoubleColumnDyn column) : base(index, column)
        {
            this.column = column;
        }

        public override SpanPair<double> GetCurrentDoubles()
        {
            //Debug.WriteLine("GetCurrentDoubles " + this.Column.Name + " " + startIndex + " " + length);

            return new SpanPair<double>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }

}
