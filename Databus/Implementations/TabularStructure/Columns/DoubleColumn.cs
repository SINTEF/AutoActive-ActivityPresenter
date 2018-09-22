using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class DoubleColumn : TableColumn
    {
        internal double[] data;
        private Task<double[]> loader;

        public DoubleColumn(string name, Task<double[]> loader, TableIndex index) : base(typeof(double), name, loader, index)
        {
            this.loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            data = loader.Result;
            return data.Length;
        }

        protected override IDataViewer CreateDoubleViewer(TableIndex index, DataViewerContext context)
        {
            return new DoubleColumnViewer(index, this, context);
        }
    }

    public class DoubleColumnViewer : TableColumnViewer
    {
        private DoubleColumn column;

        internal DoubleColumnViewer(TableIndex index, DoubleColumn column, DataViewerContext context) : base(index, column, context)
        {
            this.column = column;
        }

        public override SpanPair<double> GetCurrentDoubles()
        {
            return new SpanPair<double>(index.data.AsSpan(startIndex, endIndex), column.data.AsSpan(startIndex, endIndex));
        }
    }
}
