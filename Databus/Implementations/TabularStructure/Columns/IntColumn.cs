using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class IntColumn : TableColumn
    {
        internal int[] data;
        private Task<int[]> loader;

        public IntColumn(string name, Task<int[]> loader, TableIndex index) : base(typeof(int), name, loader, index)
        {
            this.loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            data = loader.Result;
            return data.Length;
        }

        protected override IDataViewer CreateIntViewer(TableIndex index, DataViewerContext context)
        {
            return new IntColumnViewer(index, this, context);
        }
    }

    public class IntColumnViewer : TableColumnViewer
    {
        private IntColumn column;

        internal IntColumnViewer(TableIndex index, IntColumn column, DataViewerContext context) : base(index, column, context)
        {
            this.column = column;
        }

        public override SpanPair<int> GetCurrentInts()
        {
            return new SpanPair<int>(index.data.AsSpan(startIndex, endIndex), column.data.AsSpan(startIndex, endIndex));
        }
    }
}
