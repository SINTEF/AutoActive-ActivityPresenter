using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class LongColumn : TableColumn
    {
        internal long[] data;
        private Task<long[]> loader;

        public LongColumn(string name, Task<long[]> loader, TableIndex index) : base(typeof(long), name, loader, index)
        {
            this.loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            data = loader.Result;
            return data.Length;
        }

        protected override IDataViewer CreateLongViewer(TableIndex index, DataViewerContext context)
        {
            return new LongColumnViewer(index, this, context);
        }
    }

    public class LongColumnViewer : TableColumnViewer
    {
        private LongColumn column;

        internal LongColumnViewer(TableIndex index, LongColumn column, DataViewerContext context) : base(index, column, context)
        {
            this.column = column;
        }

        public override SpanPair<long> GetCurrentLongs()
        {
            return new SpanPair<long>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }
}
