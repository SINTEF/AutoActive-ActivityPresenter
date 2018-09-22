using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class ByteColumn : TableColumn
    {
        internal byte[] data;
        private Task<byte[]> loader;

        public ByteColumn(string name, Task<byte[]> loader, TableIndex index) : base(typeof(byte), name, loader, index)
        {
            this.loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            data = loader.Result;
            return data.Length;
        }

        protected override IDataViewer CreateByteViewer(TableIndex index, DataViewerContext context)
        {
            return new ByteColumnViewer(index, this, context);
        }
    }

    public class ByteColumnViewer : TableColumnViewer
    {
        private ByteColumn column;

        internal ByteColumnViewer(TableIndex index, ByteColumn column, DataViewerContext context) : base(index, column, context)
        {
            this.column = column;
        }

        public override SpanPair<byte> GetCurrentBytes()
        {
            return new SpanPair<byte>(index.data.AsSpan(startIndex, endIndex), column.data.AsSpan(startIndex, endIndex));
        }
    }
}
