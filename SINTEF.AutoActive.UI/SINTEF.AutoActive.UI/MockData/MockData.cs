using System;
using SINTEF.AutoActive.Databus;

namespace SINTEF.AutoActive.UI.MockData
{
    public static class MockData
    {
        public static readonly DataViewerContext Context = new DataViewerContext(DataViewerRangeType.Time, 0, 10);

        public static readonly MockDataPoint Sinusoidal = new MockDataPoint(60, 0.01f, (float t) => (float)Math.Sin(2 * Math.PI * t / 2));
    }

    public class MockDataViewer : IDataViewer
    {
        float[] _data;
        int currentFirst;
        int currentLength;

        public MockDataViewer(float[] data, double start, double end, double dt, DataViewerContext context)
        {
            _data = data;
            // Handle updated range
            DataViewerRangeUpdated handler = (double from, double to) =>
            {
                // Calculate the current indicies
                int first = (int)Math.Ceiling((from - start) / dt);
                int length = (int)Math.Round((to - from) / dt);
                currentFirst = first;
                currentLength = length;
                Changed?.Invoke();
            };
            context.RangeUpdated += handler;
            handler(context.RangeFrom, context.RangeTo);
        }

        public event DataViewWasChangedHandler Changed;

        public Span<float> GetCurrentFloat()
        {
            return new Span<float>(_data, currentFirst, currentLength);
        }
    }

    public delegate float MockDataGenerator(float x);

    public class MockDataPoint : IDataPoint
    {
        public Type Type => typeof(float);

        double start = 0;
        double end;
        double step;
        float[] data;

        public MockDataPoint(float T, float dt, MockDataGenerator generator)
        {
            // Generate some data
            var N = ((long)(T / dt)) + 1;
            end = T;
            step = dt;
            data = new float[N];
            float t = 0;
            for (long i = 0; i < N; i++)
            {
                data[i] = generator(t);
                t += dt;
            }
        }

        public IDataViewer CreateViewerIn(DataViewerContext context)
        {
            return new MockDataViewer(data, start, end, step, context);
        }
    }
}
