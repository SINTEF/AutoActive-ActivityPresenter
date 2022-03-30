using SINTEF.AutoActive.Databus.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.Implementations
{
    public class BaseSortedTimePoint : BaseTimePoint
    {
        public BaseSortedTimePoint(List<long> data, bool isSynchronizedToWoldClock) : base(data, isSynchronizedToWoldClock) { }
        public override async Task<ITimeViewer> CreateViewer()
        {
            // Ensure that the data is loaded
            await CreateViewer();
            var viewer = new BaseSortedTimeViewer(this);
            _viewers.Add(viewer);
            return viewer;
        }
    }

    public class BaseSortedTimeViewer : ITimeViewer
    {
        protected readonly BaseTimePoint _timePoint;
        public BaseSortedTimeViewer(BaseTimePoint timePoint)
        {
            _timePoint = timePoint;
        }
        public long Start => _timePoint.Data[0];

        public long End => _timePoint.Data[_timePoint.Data.Count - 1];

        public ITimePoint TimePoint => _timePoint;

        public event TimeViewerWasChangedHandler TimeChanged;

        public void UpdatedTimeIndex()
        {
            TimeChanged?.Invoke(this, Start, End);
        }
    }
}
