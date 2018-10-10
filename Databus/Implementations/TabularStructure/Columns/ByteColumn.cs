using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class ByteColumn : TableColumn
    {
        internal byte[] data;
        private Task<byte[]> loader;

        public ByteColumn(string name, Task<byte[]> loader, TableTimeIndex index) : base(typeof(byte), name, loader, index)
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

        protected override IDataViewer CreateByteViewer(TableTimeIndex index)
        {
            return new ByteColumnViewer(index, this);
        }
    }

    public class ByteColumnViewer : TableColumnViewer
    {
        private ByteColumn column;

        internal ByteColumnViewer(TableTimeIndex index, ByteColumn column) : base(index, column)
        {
            this.column = column;
        }

        public override SpanPair<byte> GetCurrentBytes()
        {
            return new SpanPair<byte>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }
}
