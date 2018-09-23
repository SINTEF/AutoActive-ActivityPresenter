using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class BoolColumn : TableColumn
    {
        internal bool[] data;
        private Task<bool[]> loader;

        public BoolColumn(string name, Task<bool[]> loader, TableIndex index) : base(typeof(bool), name, loader, index)
        {
            this.loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            data = loader.Result;
            return data.Length;
        }

        protected override IDataViewer CreateBoolViewer(TableIndex index, DataViewerContext context)
        {
            return new BoolColumnViewer(index, this, context);
        }

        protected override (double? min, double? max) GetDataMinMax()
        {
            return (0, 1);
        }
    }

    public class BoolColumnViewer : TableColumnViewer
    {
        private BoolColumn column;

        internal BoolColumnViewer(TableIndex index, BoolColumn column, DataViewerContext context) : base(index, column, context)
        {
            this.column = column;
        }

        public override SpanPair<bool> GetCurrentBools()
        {
            return new SpanPair<bool>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }
}
