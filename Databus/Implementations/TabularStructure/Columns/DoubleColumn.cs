using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class DoubleColumn : TableColumn
    {
        internal double[] data;
        private Task<double[]> loader;

        public DoubleColumn(string name, Task<double[]> loader, TableTimeIndex index) : base(typeof(double), name, loader, index)
        {
            this.loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            data = loader.Result;
            return data.Length;
        }

        protected override (double? min, double? max) GetDataMinMax()
        {
            if (data.Length == 0) return (null, null);
            var min = data[0];
            var max = data[0];
            for (var i = 1; i < data.Length; i++)
            {
                if (data[i] < min) min = data[i];
                if (data[i] > max) max = data[i];
            }
            return (min, max);
        }

        protected override IDataViewer CreateDoubleViewer(TableTimeIndex index)
        {
            return new DoubleColumnViewer(index, this);
        }
    }

    public class DoubleColumnViewer : TableColumnViewer
    {
        private DoubleColumn column;

        internal DoubleColumnViewer(TableTimeIndex index, DoubleColumn column) : base(index, column)
        {
            this.column = column;
        }

        public override SpanPair<double> GetCurrentDoubles()
        {
            return new SpanPair<double>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }
}
