using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class BoolColumn : TableColumn
    {
        internal bool[] data;
        private Task<bool[]> loader;

        public BoolColumn(string name, Task<bool[]> loader, TableTimeIndex index) : base(typeof(bool), name, loader, index)
        {
            this.loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            data = loader.Result;
            return data.Length;
        }

        protected override IDataViewer CreateBoolViewer(TableTimeIndex index)
        {
            return new BoolColumnViewer(index, this);
        }

        protected override (double? min, double? max) GetDataMinMax()
        {
            return (0, 1);
        }
    }

    public class BoolColumnViewer : TableColumnViewer
    {
        private BoolColumn column;

        internal BoolColumnViewer(TableTimeIndex index, BoolColumn column) : base(index, column)
        {
            this.column = column;
        }

        public override SpanPair<bool> GetCurrentBools()
        {
            return new SpanPair<bool>(Index.Data.AsSpan(StartIndex, Length), column.data.AsSpan(StartIndex, Length));
        }
    }
}
