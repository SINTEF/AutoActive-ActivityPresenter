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

        SpanPair<byte> GetCurrentBytes();
        SpanPair<int> GetCurrentInts();
        SpanPair<long> GetCurrentLongs();

        SpanPair<float> GetCurrentFloats();
        SpanPair<double> GetCurrentDoubles();

        SpanPair<string> GetCurrentStrings();
    }

    /* -- Helper struct for carrying both time and data in a single Span-like structure -- */
    public readonly ref struct SpanPair<T>
    {
        public SpanPair(Span<long> X, Span<T> Y)
        {
            this.X = X;
            this.Y = Y;
        }

        public readonly Span<long> X;
        public readonly Span<T> Y;

        public Enumerator GetEnumerator(int maxItems) => new Enumerator(X, Y, maxItems);

        public ref struct Enumerator
        {
            private readonly Span<long> _x;
            private readonly Span<T> _y;
            private int _index;
            private readonly int _decimator;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(Span<long> x, Span<T> y, int maxItems)
            {
                _x = x;
                _y = y;
                _decimator = maxItems > 0 ? _x.Length / (maxItems + 1) + 1 : 1;
                _index = -_decimator;
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
