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
            return new SpanPair<int>(index.data.AsSpan(startIndex, length), column.data.AsSpan(startIndex, length));
        }
    }
}
