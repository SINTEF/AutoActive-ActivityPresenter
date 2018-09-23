using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class StringColumn : TableColumn
    {
        internal string[] data;
        private Task<string[]> loader;

        public StringColumn(string name, Task<string[]> loader, TableIndex index) : base(typeof(string), name, loader, index)
        {
            this.loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            data = loader.Result;
            return data.Length;
        }

        protected override IDataViewer CreateStringViewer(TableIndex index, DataViewerContext context)
        {
            return new StringColumnViewer(index, this, context);
        }

        protected override (double? min, double? max) GetDataMinMax()
        {
            return (null, null);
        }
    }

    public class StringColumnViewer : TableColumnViewer
    {
        private StringColumn column;

        internal StringColumnViewer(TableIndex index, StringColumn column, DataViewerContext context) : base(index, column, context)
        {
            this.column = column;
        }

        public override SpanPair<string> GetCurrentStrings()
        {
            return new SpanPair<string>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }
}
