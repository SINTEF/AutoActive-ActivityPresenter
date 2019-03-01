using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class DoubleColumn : TableColumn
    {
        internal double[] Data;
        private readonly Task<double[]> _loader;

        public DoubleColumn(string name, Task<double[]> loader, TableTimeIndex index) : base(typeof(double), name, loader, index)
        {
            _loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            Data = _loader.Result;
            return Data.Length;
        }

        protected override (double? min, double? max) GetDataMinMax()
        {
            if (Data.Length == 0) return (null, null);
            var min = Data[0];
            var max = Data[0];
            for (var i = 1; i < Data.Length; i++)
            {
                if (Data[i] < min) min = Data[i];
                if (Data[i] > max) max = Data[i];
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
        private readonly DoubleColumn _column;

        internal DoubleColumnViewer(TableTimeIndex index, DoubleColumn column) : base(index, column)
        {
            _column = column;
        }

        public override SpanPair<double> GetCurrentDoubles()
        {
            return new SpanPair<double>(Index.Data.AsSpan(StartIndex, Length), _column.Data.AsSpan(StartIndex, Length));
        }
    }
}
