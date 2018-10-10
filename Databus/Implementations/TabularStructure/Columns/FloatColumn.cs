using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class FloatColumn : TableColumn
    {
        internal float[] data;
        private Task<float[]> loader;

        public FloatColumn(string name, Task<float[]> loader, TableTimeIndex index) : base(typeof(float), name, loader, index)
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
            return new SpanPair<float>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }
}
