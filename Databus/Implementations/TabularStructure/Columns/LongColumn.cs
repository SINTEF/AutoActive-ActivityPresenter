using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class LongColumn : TableColumn
    {
        internal long[] Data { get; private set; }
        private readonly Task<long[]> _loader;

        public LongColumn(string name, Task<long[]> loader, TableTimeIndex index) : base(typeof(long), name, loader, index)
        {
            _loader = loader;
        }

        protected override int CheckLoaderResultLength()
        {
            Data = _loader.Result;
            return Data.Length;
        }

        protected override (double? min, double? max) GetDataMinMax()
        {
            if (Data.Length == 0) return (null, null);
            var min = Data[0];
            var max = Data[0];
            for (var i = 1; i < Data.Length; i++)
            {
                if (Data[i] < min) min = Data[i];
                if (Data[i] > max) max = Data[i];
            }
            return (min, max);
        }

        protected override IDataViewer CreateLongViewer(TableTimeIndex index)
        {
            return new LongColumnViewer(index, this);
        }
    }

    public class LongColumnViewer : TableColumnViewer
    {
        private readonly LongColumn _column;

        internal LongColumnViewer(TableTimeIndex index, LongColumn column) : base(index, column)
        {
            _column = column;
        }

        public override SpanPair<long> GetCurrentLongs()
        {
            return new SpanPair<long>(Index.Data.AsSpan(StartIndex, Length), _column.Data.AsSpan(StartIndex, Length));
        }
    }
}
