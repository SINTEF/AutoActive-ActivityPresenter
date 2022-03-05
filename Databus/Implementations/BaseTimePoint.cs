using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations
{
    public class BaseTimePoint : ITimePoint
    {
        protected readonly List<ITimeViewer> _viewers = new List<ITimeViewer>();
        public bool IsSynchronizedToWorldClock { get; set; }

        public List<long> Data { get; private set; }
        public Task<List<long>> DataLoader;
        protected bool _isDataLoaded = false;

        public BaseTimePoint(List<long> data, bool isSynchronizedToWorldClock)
        {
            Data = data;
            _isDataLoaded = true;
            IsSynchronizedToWorldClock = isSynchronizedToWorldClock;
        }

        public BaseTimePoint(Task<List<long>> dataLoader, bool isSynchronizedToWorldClock)
        {
            _isDataLoaded = true;
            DataLoader = dataLoader;
            IsSynchronizedToWorldClock = isSynchronizedToWorldClock;
        }

        public virtual async Task EnsureData()
        {
            if (_isDataLoaded)
            {
                return;
            }

            Data = await DataLoader;
            _isDataLoaded = true;
        }

        public virtual async Task<ITimeViewer> CreateViewer()
        {
            // Ensure that the data is loaded
            await EnsureData();
            var viewer = new BaseTimeViewer(this);
            _viewers.Add(viewer);
            return viewer;
        }

        public void TransformTime(long offset, double scale)
        {
            for (var i = 0; i < Data.Count; i++)
            {
                Data[i] = (long)(Data[i] * scale + offset);
            }

            foreach (var viewer in _viewers)
            {
                viewer.UpdatedTimeIndex();
            }
        }

        private long? _start;
        public virtual long Start
        {
            get
            {
                if (!_start.HasValue)
                {
                    if (Data.Count == 0)
                    {
                        return 0;
                    }
                    _start = Data.Min();
                }
                return _start.Value;
            }
        }

        private long? _end;
        public virtual long End
        {
            get
            {
                if (!_end.HasValue)
                {
                    if (Data.Count == 0)
                    {
                        return 0;
                    }
                    _end = Data.Max();
                }
                return _end.Value;
            }
        }

        public void TriggerChanged()
        {
            foreach(var viewer in _viewers)
            {
                viewer.UpdatedTimeIndex();
            }
        }
    }

    public class BaseTimeViewer : ITimeViewer
    {
        public List<long> Data => _timePoint.Data;
        public BaseTimeViewer(BaseTimePoint timePoint)
        {
            _timePoint = timePoint;
        }

        private BaseTimePoint _timePoint;
        public ITimePoint TimePoint => _timePoint;

        public long Start => TimePoint.Start;

        public long End => TimePoint.End;

        public event TimeViewerWasChangedHandler TimeChanged;

        public virtual void UpdatedTimeIndex()
        {
            TimeChanged?.Invoke(this, Start, End);
        }
    }
}
