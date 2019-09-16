using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Runtime.CompilerServices;

namespace SINTEF.AutoActive.Databus.Common
{
    public interface ITimeSeriesViewer : IDataViewer
    {
        double? MinValueHint { get; }
        double? MaxValueHint { get; }

        SpanPair<bool> GetCurrentBools();

        SpanPair<string> GetCurrentStrings();

        SpanPair<T> GetCurrentData<T>() where T : IConvertible;
    }

    /* -- Helper struct for carrying both time and data in a single Span-like structure -- */
    public readonly ref struct SpanPair<T>
    {
        private readonly int _startOffset;
        public SpanPair(int startOffset, Span<long> x, Span<T> y)
        {
            _startOffset = startOffset;
            X = x;
            Y = y;
        }

        public readonly Span<long> X;
        public readonly Span<T> Y;

        public Enumerator GetEnumerator(int maxItems) => new Enumerator(_startOffset, X, Y, maxItems);

        public ref struct Enumerator
        {
            private readonly Span<long> _x;
            private readonly Span<T> _y;
            private int _index;
            private readonly int _decimator;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(int startOffset, Span<long> x, Span<T> y, int maxItems)
            {
                _x = x;
                _y = y;
                _decimator = maxItems > 0 ? _x.Length / maxItems : 1;
                if (_decimator < 1)
                {
                    _decimator = 1;
                    _index = -_decimator;
                }
                else
                {
                    _index = -(startOffset % _decimator);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                var index = _index + _decimator;

                if (index >= _x.Length || index >= _y.Length)
                {
                    // Always keep the last element (to fill the plot)
                    if (_index != _x.Length - 1)
                    {
                        _index = _x.Length - 1;
                        return true;
                    }
                    return false;
                }

                _index = index;
                return true;
            }

            public (long x, T y) Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (_x[_index], _y[_index]);
            }


        }
    }
}
