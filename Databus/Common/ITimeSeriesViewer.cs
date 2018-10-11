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

        public Enumerator GetEnumerator() => new Enumerator(X, Y);

        public ref struct Enumerator
        {
            private readonly Span<long> _x;
            private readonly Span<T> _y;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(Span<long> x, Span<T> y)
            {
                _x = x;
                _y = y;
                _index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                int index = _index + 1;
                if (index < _x.Length && index < _y.Length)
                {
                    _index = index;
                    return true;
                }
                return false;
            }

            public (long x, T y) Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (_x[_index], _y[_index]);
            }


        }
    }
}
