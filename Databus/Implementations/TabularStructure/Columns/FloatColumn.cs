using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class FloatColumn : TableColumn
    {
        internal float[] data;
        private Task<float[]> loader;

        public FloatColumn(string name, Task<float[]> loader, TableIndex index) : base(typeof(float), name, loader, index)
        {
            this.loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            data = loader.Result;
            return data.Length;
        }

        protected override IDataViewer CreateFloatViewer(TableIndex index, DataViewerContext context)
        {
            return new FloatColumnViewer(index, this, context);
        }
    }

    public class FloatColumnViewer : TableColumnViewer
    {
        private FloatColumn column;

        internal FloatColumnViewer(TableIndex index, FloatColumn column, DataViewerContext context) : base(index, column, context)
        {
            this.column = column;
        }

        public override SpanPair<float> GetCurrentFloats()
        {
            return new SpanPair<float>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }
}
