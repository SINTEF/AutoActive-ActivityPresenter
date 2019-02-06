using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Interfaces;

namespace SINTEF.AutoActive.Databus.ViewerContext
{
    public class TimeSynchronizedContext : SingleSetDataViewerContext
    {
        private ITimeViewer _currentMinViewer;
        private ITimeViewer _currentMaxViewer;

        protected override void OnTimeViewerAvailableChanged(ITimeViewer sender, long start, long end)
        {
            //Debug.WriteLine($"TimeSynchronizedContext.OnTimeViewerAvailbleChanged({sender}, {start}, {end})");
            var newFrom = AvailableTimeFrom;
            var newTo = AvailableTimeTo;
            var changedStart = IsSynchronizedToWorldClock ? start : 0;
            var changedEnd = IsSynchronizedToWorldClock ? end : end - start;
            var needToRecalculate = false;

            // Handle minimum available time
            if (_currentMinViewer == null || changedStart < AvailableTimeFrom)
            {
                _currentMinViewer = sender;
                newFrom = changedStart;
            }
            else if (_currentMinViewer == sender && changedStart > AvailableTimeFrom)
            {
                // We were defining the minimum, but our time range was reduced, so we need to recalculate
                needToRecalculate = true;
            }

            // Handle maximum available time
            if (_currentMaxViewer == null || changedEnd > AvailableTimeTo)
            {
                _currentMaxViewer = sender;
                newTo = changedEnd;
            }
            else if (_currentMaxViewer == sender && changedEnd < AvailableTimeTo)
            {
                // We were defining the maximum, but our time range was reduced, so we need to recalculate
                needToRecalculate = true;
            }

            if (needToRecalculate)
                GetAvailableTimeMinMax(IsSynchronizedToWorldClock, out _currentMinViewer, out newFrom, out _currentMaxViewer, out newTo);

            InternalSetAvailableTimeRange(newFrom, newTo);
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
            if (InternalSetSynchronizedToWorldClock(value))
            {
                // Synchronization changed, we should re-calculate available time range
                GetAvailableTimeMinMax(value, out _currentMinViewer, out var from, out _currentMaxViewer, out var to);
                InternalSetAvailableTimeRange(from, to);
            }
        }
    }
}
