using System;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;

namespace SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns
{
    public class GenericColumn<T> : TableColumn where T : IConvertible
    {
        internal T[] Data;
        private readonly Task<T[]> _loader;
        public GenericColumn(string name, Task<T[]> loader, TableTimeIndex index, string uri, string unit) : base(typeof(T), name, loader, index, uri)
        {
            _loader = loader;
            Unit = unit;
        }

        protected override int CheckLoaderResultLength()
        {
            Data = _loader.Result;
            return Data.Length;
        }

        protected override (double? min, double? max) GetDataMinMax()
        {
            //changed to handle NaN in the data
            if (Data.Length == 0) return (null, null);
            double min = Convert.ToDouble(Data[0]);
            double max = Convert.ToDouble(Data[0]);
            for (var i = 0; i < Data.Length; i++)
            {
                var el = Convert.ToDouble(Data[i]);
                if (Double.IsNaN(el)) continue;
                //Ensures that we never compare a number with a NaN,
                //since the result will be a NaN
                if (!Double.IsNaN(min))
                {
                    if (el < min) min = el;
                }
                else
                {
                    min = el;
                }
                if (!Double.IsNaN(max))
                {
                    if (el > max) max = el;
                }
                else
                {
                    max = el;
                }
            }
            return (min, max);
        }

        protected override IDataViewer CreateGenericViewer(TableTimeIndex index)
        {
            return new GenericColumnViewer<T>(index, this);
        }
    }

    public class GenericColumnViewer<T> : TableColumnViewer where T : IConvertible
    {
        private readonly GenericColumn<T> _column;
        public GenericColumnViewer(TableTimeIndex index, GenericColumn<T> column) : base(index, column)
        {
            _column = column;
        }

        public override SpanPair<T1> GetCurrentData<T1>()
        {
            if (typeof(T1) != typeof(T))
                throw new ArgumentException();
            if (Length <= 0) return new SpanPair<T1>();

            Span<T1> data;
            unsafe
            {
                var mem = _column.Data.AsMemory(StartIndex, Length);
                using (var pin = mem.Pin())
                    data = new Span<T1>(pin.Pointer, Length);
            }

            return new SpanPair<T1>(StartIndex, Index.Data.AsSpan(StartIndex, Length), data);
        }
    }
}
