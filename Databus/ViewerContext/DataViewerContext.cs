using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.ViewerContext
{
    // FIXME: Do we need thread-safety in these classes?

    public delegate void DataViewerContextWorldClockChangedHandler(DataViewerContext sender, bool synchronizedToWorldClock);
    public delegate void DataViewerContextAvailableTimeRangeChangedHandler(DataViewerContext sender, long from, long to);

    public abstract class DataViewerContext
    {
        // --- Clock mode ---
        public bool IsSynchronizedToWorldClock { get; private set; }
        public event DataViewerContextWorldClockChangedHandler SynchronizedToWorldClockChanged;
        protected bool InternalSetSynchronizedToWorldClock(bool value)
        {
            if (value != IsSynchronizedToWorldClock)
            {
                IsSynchronizedToWorldClock = value;
                SynchronizedToWorldClockChanged?.Invoke(this, value);
                return true;
            }
            return false;
        }

        // --- Available time range ---
        public long AvailableTimeFrom { get; private set; }
        public long AvailableTimeTo { get; private set; }
        public event DataViewerContextAvailableTimeRangeChangedHandler AvailableTimeRangeChanged;
        protected bool InternalSetAvailableTimeRange(long from, long to)
        {
            if (AvailableTimeFrom != from || AvailableTimeTo != to)
            {
                AvailableTimeFrom = from;
                AvailableTimeTo = to;
                AvailableTimeRangeChanged?.Invoke(this, from, to);
                return true;
            }
            return false;
        }
    }

    public delegate void DataViewerContextSelectedRangeChangedHandler(SingleSetDataViewerContext sender, long from, long to);

    public abstract class SingleSetDataViewerContext : DataViewerContext
    {
        // ---- Data viewers ----
        private readonly Dictionary<ITimeViewer, List<IDataViewer>> viewers = new Dictionary<ITimeViewer, List<IDataViewer>>();
        //private readonly ISet<ITimeViewer> timeviewers = new HashSet<ITimeViewer>();
        //private readonly ISet<IDataViewer> dataviewers = new HashSet<IDataViewer>();

        private void SetTimeRangeForViewer(ITimeViewer timeviewer, IDataViewer dataviewer, long from, long to)
        {
            if (IsSynchronizedToWorldClock) dataviewer.SetTimeRange(from, to);
            else dataviewer.SetTimeRange(from + timeviewer.Start, to + timeviewer.Start);
        }

        protected async Task<IDataViewer> InternalGetOrAddViewerFor(IDataPoint datapoint)
        {
            foreach (var timeDataViewers in viewers)
            {
                var timeviewer = timeDataViewers.Key;
                if (timeviewer.TimePoint == datapoint.Time)
                {
                    // We already have a time viewer for this datapoint, check if we also have a dataviewer
                    foreach (var dataviewer in timeDataViewers.Value)
                    {
                        if (dataviewer.DataPoint == datapoint)
                        {
                            return dataviewer;
                        }
                    }
                    // If not, create one
                    var newViewer = await datapoint.CreateViewer();
                    timeDataViewers.Value.Add(newViewer);
                    SetTimeRangeForViewer(timeviewer, newViewer, SelectedTimeFrom, SelectedTimeTo);
                }
            }

            // If we don't have a timeviewer, we need to create both
            var newDataviewer = await datapoint.CreateViewer();
            var newTimeviewer = await datapoint.Time.CreateViewer();
            viewers.Add(newTimeviewer, new List<IDataViewer> { newDataviewer });
            newTimeviewer.TimeChanged += OnTimeViewerAvailableChanged;
            OnTimeViewerAvailableChanged(newTimeviewer, newTimeviewer.Start, newTimeviewer.End);

            return newDataviewer;

            //// Check if we already have a viewer for this datapoint
            //foreach (var existingViewer in dataviewers)
            //{
            //    if (existingViewer.DataPoint == datapoint)
            //        return existingViewer;
            //}

            //// If not, we need to create a new one
            //var viewer = await datapoint.CreateViewer();
            //viewer.SetTimeRange(SelectedTimeFrom, SelectedTimeTo);
            //dataviewers.Add(viewer);

            //// Check if we have a timeviewer for the datapoints time
            //var timeViewerFound = false;
            //foreach (var existingViewer in timeviewers)
            //{
            //    if (existingViewer.TimePoint == datapoint.Time)
            //    {
            //        timeViewerFound = true;
            //        break;
            //    }
            //}

            //// If not, we need to create on of those as well
            //if (!timeViewerFound)
            //{
            //    var timeviewer = await viewer.DataPoint.Time.CreateViewer();
            //    timeviewers.Add(timeviewer);
            //    timeviewer.Changed += OnTimeViewerAvailableChanged;
            //    OnTimeViewerAvailableChanged(timeviewer, timeviewer.Start, timeviewer.End);
            //}
            
            //return viewer;
        }

        protected abstract void OnTimeViewerAvailableChanged(ITimeViewer sender, long start, long end);

        // --- Selected time range ---
        public long SelectedTimeFrom { get; private set; } = 0;
        public long SelectedTimeTo { get; private set; } = 0;
        public event DataViewerContextSelectedRangeChangedHandler SelectedTimeRangeChanged;
        protected void InternalSetSelectedTimeRange(long from, long to)
        {
            if (SelectedTimeFrom != from || SelectedTimeTo != to)
            {
                SelectedTimeFrom = from;
                SelectedTimeTo = to;

                // TODO: Maybe we should set the mapping instead of doing this every time
                foreach (var timeDataViewers in viewers)
                {
                    var timeviewer = timeDataViewers.Key;
                    foreach (var dataviewer in timeDataViewers.Value)
                        SetTimeRangeForViewer(timeviewer, dataviewer, from, to);
                }

                SelectedTimeRangeChanged?.Invoke(this, from, to);
            }
        }

        // --- Available time range based on current time viewers ---
        protected void GetAvailableTimeMinMax(bool useWorldClock, out ITimeViewer minViewer, out long min, out ITimeViewer maxViewer, out long max)
        {
            if (viewers.Count == 0)
            {
                minViewer = maxViewer = null;
                min = max = 0;
                return;
            }
            
            // Calculate the min max of current timeviewers
            minViewer = maxViewer = null;
            min = long.MaxValue;
            max = long.MinValue;
            
            foreach (var timeDataviewers in viewers)
            {
                var timeviewer = timeDataviewers.Key;
                var start = useWorldClock ? timeviewer.Start : 0;
                var end = useWorldClock ? timeviewer.End : timeviewer.End - timeviewer.Start;
                if (start < min)
                {
                    min = start;
                    minViewer = timeviewer;
                }
                if (end > max)
                {
                    max = end;
                    maxViewer = timeviewer;
                }
            }

            //if (timeviewers.Count == 0)
            //{
            //    minViewer = maxViewer = null;
            //    min = max = 0;
            //    return;
            //}

            //// Calculate the min max of current timeviewers
            //minViewer = maxViewer = null;
            //min = long.MaxValue;
            //max = long.MinValue;

            //foreach (var timeviewer in timeviewers)
            //{
            //    var start = useWorldClock ? timeviewer.Start : 0;
            //    var end = useWorldClock ? timeviewer.End : timeviewer.End - timeviewer.Start;
            //    if (start < min)
            //    {
            //        min = start;
            //        minViewer = timeviewer;
            //    }
            //    if (end > max)
            //    {
            //        max = end;
            //        maxViewer = timeviewer;
            //    }
            //}
        }
    }
}
