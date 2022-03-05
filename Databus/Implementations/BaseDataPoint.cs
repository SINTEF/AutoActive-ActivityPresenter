using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations
{
    // To simplify generic type matching
    public interface IBaseDataPoint : IDataPoint { }

    public class BaseDataPoint<T> : IBaseDataPoint where T : IConvertible
    {
        public List<T> Data { get; protected set; }
        protected Task<List<T>> DataLoader;
        public BaseDataPoint(string name, Task<List<T>> loader, BaseTimePoint time, string uri, string unit)
        {
            Name = name;
            DataLoader = loader;
            URI = uri;
            DataType = typeof(T);
            Time = time;
            Unit = unit;
        }

        public BaseDataPoint(string name, List<T> data, BaseTimePoint time, string uri, string unit)
        {
            Data = data;
            URI = uri;
            DataType = typeof(T);
            Name = name;
            Time = time;
            Unit = unit;
        }
        public string URI { get; set; }

        public Type DataType { get; }

        public string Name { get; set; }

        public BaseTimePoint Time { get; }
        ITimePoint IDataPoint.Time => Time;

        public string Unit { get; set; }

        private List<BaseDataViewer> _viewers = new List<BaseDataViewer>();
        public bool HasViewers() { 
            return _viewers.Count > 0;
        }

        public event EventHandler DataChanged;

        protected virtual BaseDataViewer CreateDataViewer()
        {
            return new BaseDataViewer(new BaseTimeViewer(Time), this);
        }

        public async Task<IDataViewer> CreateViewer()
        {
            if (Data == null && DataLoader != null)
            {
                Data = await DataLoader;
            }

            // TODO: keep list of viewers?
            var viewer = CreateDataViewer();
            _viewers.Add(viewer);
            return viewer;
        }

        public void TriggerDataChanged()
        {
            foreach(var viewer in _viewers)
            {
                viewer.TriggerDataChanged();
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class BaseDataViewer : IDataViewer
    {
        internal ITimeViewer TimeViewer;
        public BaseDataViewer(ITimeViewer timeViewer, IDataPoint dataPoint)
        {
            TimeViewer = timeViewer;
            DataPoint = dataPoint;
        }
        public IDataPoint DataPoint { get; internal set; }

        public long CurrentTimeRangeFrom { get; private set; }

        public long CurrentTimeRangeTo { get; private set; }

        public long PreviewPercentage { get; set; }

        public event EventHandler Changed;

        public void SetTimeRange(long from, long to)
        {
            var diff = to - from;
            var startTime = from - diff * PreviewPercentage / 100;
            var endTime = from + diff;

            CurrentTimeRangeFrom = from;
            CurrentTimeRangeTo = to;

            /*
            var start = TimeViewer.FindIndex(StartIndex, startTime);
            var end = Index.FindIndex(EndIndex, endTime);

            if (start == StartIndex && end == EndIndex) return;

            StartIndex = start;
            EndIndex = end;
            Length = EndIndex - StartIndex + 1;
            */
            Changed?.Invoke(this, EventArgs.Empty);
        }

        internal void TriggerDataChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public class BaseTimeSeriesViewer<T> : BaseDataViewer, ITimeSeriesViewer where T : IConvertible
    {
        private readonly BaseDataPoint<T> _dataPoint;
        private readonly BaseTimeViewer _timeViewer;
        public BaseTimeSeriesViewer(BaseTimeViewer timeViewer, BaseDataPoint<T> dataPoint) : base(timeViewer, dataPoint)
        {
            _dataPoint = dataPoint;
            _timeViewer = timeViewer;
        }

        public double? MinValueHint => null;

        public double? MaxValueHint => null;

        public virtual List<T> GetSelectedData()
        {
            return _dataPoint.Data;
        }


        public SpanPair<T2> GetCurrentData<T2>() where T2 : IConvertible
        {
            if (typeof(T2) != typeof(T))
                throw new ArgumentException();
            if (_dataPoint.Data.Count <= 0) return new SpanPair<T2>();


            var elements = GetSelectedData();

            var startIndex = 0;

            Span<T2> data;
            unsafe
            {
                var mem = elements.ToArray().AsMemory(0, elements.Count);
                using (var pin = mem.Pin())
                    data = new Span<T2>(pin.Pointer, elements.Count);
            }
            return new SpanPair<T2>(startIndex, _timeViewer.Data.ToArray().AsSpan(startIndex, elements.Count), data);
        }

        public SpanPair<bool> GetCurrentBools() { throw new NotImplementedException(); }
        public SpanPair<string> GetCurrentStrings() { throw new NotImplementedException(); }
    }
}
