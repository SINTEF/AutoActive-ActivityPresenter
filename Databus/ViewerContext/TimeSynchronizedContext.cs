using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Interfaces;

namespace SINTEF.AutoActive.Databus.ViewerContext
{
    public class TimeSynchronizedContext : SingleSetDataViewerContext
    {
        ITimeViewer currentMinViewer = null;
        ITimeViewer currentMaxViewer = null;

        protected override void OnTimeViewerAvailableChanged(ITimeViewer sender, long start, long end)
        {
            //Debug.WriteLine($"TimeSynchronizedContext.OnTimeViewerAvailbleChanged({sender}, {start}, {end})");
            var newFrom = AvailableTimeFrom;
            var newTo = AvailableTimeTo;
            var changedStart = IsSynchronizedToWorldClock ? start : 0;
            var changedEnd = IsSynchronizedToWorldClock ? end : end - start;
            var needToRecalculate = false;

            // Handle minimum available time
            if (currentMinViewer == null || changedStart < AvailableTimeFrom)
            {
                currentMinViewer = sender;
                newFrom = changedStart;
            }
            else if (currentMinViewer == sender && changedStart > AvailableTimeFrom)
            {
                // We were defining the minimum, but our time range was reduced, so we need to recalculate
                needToRecalculate = true;
            }

            // Handle maximum available time
            if (currentMaxViewer == null || changedEnd > AvailableTimeTo)
            {
                currentMaxViewer = sender;
                newTo = changedEnd;
            }
            else if (currentMaxViewer == sender && changedEnd < AvailableTimeTo)
            {
                // We were defining the maximum, but our time range was reduced, so we need to recalculate
                needToRecalculate = true;
            }

            if (needToRecalculate)
                GetAvailableTimeMinMax(IsSynchronizedToWorldClock, out currentMinViewer, out newFrom, out currentMaxViewer, out newTo);

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
                GetAvailableTimeMinMax(value, out currentMinViewer, out var from, out currentMaxViewer, out var to);
                InternalSetAvailableTimeRange(from, to);
            }
        }
    }
}
