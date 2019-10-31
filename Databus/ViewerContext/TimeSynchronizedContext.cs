using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Interfaces;

namespace SINTEF.AutoActive.Databus.ViewerContext
{
    public class TimeSynchronizedContext : SingleSetDataViewerContext
    {
        protected override void OnTimeViewerAvailableChanged(ITimeViewer sender, long start, long end)
        {
            var (min, max) = GetAvailableTimeMinMax(IsSynchronizedToWorldClock);
            InternalSetAvailableTimeRange(min, max);
        }

        // --- Public API ---
        public Task<IDataViewer> GetDataViewerFor(IDataPoint datapoint)
        {
            return InternalGetOrAddViewerFor(datapoint);
        }

        public void SetSelectedTimeRange(long? from, long? to)
        {
            var newFrom = from ?? SelectedTimeFrom;
            var newTo = to ?? SelectedTimeTo;
            InternalSetSelectedTimeRange(newFrom, newTo);
        }

        public void SetSynchronizedToWorldClock(bool value)
        {
            IsSynchronizedToWorldClock = value;

            // Synchronization changed, we should re-calculate available time range
            var (from, to) = GetAvailableTimeMinMax(value);
            InternalSetAvailableTimeRange(from, to);
        }

        public virtual (long, long) GetAvailableTimeInContext(ITimeViewer view)
        {
            return (view.Start, view.End);
        }
    }
}
