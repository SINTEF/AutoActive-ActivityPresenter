using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class FloatColumn : TableColumn
    {
        internal float[] Data;
        private readonly Task<float[]> _loader;

        public FloatColumn(string name, Task<float[]> loader, TableTimeIndex index) : base(typeof(float), name, loader, index)
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

        protected override IDataViewer CreateFloatViewer(TableTimeIndex index)
        {
            return new FloatColumnViewer(index, this);
        }
    }

    public class FloatColumnViewer : TableColumnViewer
    {
        private FloatColumn column;

        internal FloatColumnViewer(TableTimeIndex index, FloatColumn column) : base(index, column)
        {
            this.column = column;
        }

        public override SpanPair<float> GetCurrentFloats()
        {
            return new SpanPair<float>(Index.Data.AsSpan(StartIndex, Length), column.Data.AsSpan(StartIndex, Length));
        }
    }
}
