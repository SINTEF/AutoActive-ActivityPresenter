using System;
using SINTEF.AutoActive.Databus.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus.ViewerContext
{
    // FIXME: Do we need thread-safety in these classes?

    public delegate void DataViewerContextWorldClockChangedHandler(DataViewerContext sender, bool synchronizedToWorldClock);
    public delegate void DataViewerContextAvailableTimeRangeChangedHandler(DataViewerContext sender, long from, long to);

    public abstract class DataViewerContext
    {
        // --- Clock mode ---
        private bool _isSynchronizedToWorldClock = true;
        protected bool IsSynchronizedToWorldClock
        {
            get => _isSynchronizedToWorldClock;
            set
            {
                _isSynchronizedToWorldClock = value;
                SynchronizedToWorldClockChanged?.Invoke(this, value);
            }
        }

        public event DataViewerContextWorldClockChangedHandler SynchronizedToWorldClockChanged;

        // --- Available time range ---
        public virtual long AvailableTimeFrom { get; protected set; }
        public virtual long AvailableTimeTo { get; protected set; }
        public event DataViewerContextAvailableTimeRangeChangedHandler AvailableTimeRangeChanged;
        protected bool InternalSetAvailableTimeRange(long from, long to)
        {
            if (AvailableTimeFrom == from && AvailableTimeTo == to) return false;

            AvailableTimeFrom = from;
            AvailableTimeTo = to;
            AvailableTimeRangeChanged?.Invoke(this, from, to);
            return true;
        }
        public long SelectedTimeFrom { get; protected set; }
        public long SelectedTimeTo { get; protected set; }

        private bool _isPlaying;
        private double _playbackRate;

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;
                IsPlayingChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<bool> IsPlayingChanged;

        public double PlaybackRate
        {
            get => _playbackRate;
            set
            {
                _playbackRate = value;
                PlaybackRateChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<double> PlaybackRateChanged;
    }

    public delegate void DataViewerContextSelectedRangeChangedHandler(SingleSetDataViewerContext sender, long from, long to);

    public abstract class SingleSetDataViewerContext : DataViewerContext
    {
        private readonly List<SingleSetDataViewerContext> AssociatedContext = new List<SingleSetDataViewerContext>();

        // ---- Data _viewers ----
        private readonly Dictionary<ITimeViewer, List<IDataViewer>> _viewers =
            new Dictionary<ITimeViewer, List<IDataViewer>>();

        private void SetTimeRangeForViewer(ITimeViewer timeviewer, IDataViewer dataviewer, long from, long to)
        {
            if (IsSynchronizedToWorldClock) dataviewer.SetTimeRange(from, to);
            else dataviewer.SetTimeRange(from + timeviewer.Start, to + timeviewer.Start);
        }

        protected async Task<IDataViewer> InternalGetOrAddViewerFor(IDataPoint datapoint)
        {
            foreach (var timeDataViewers in _viewers)
            {
                var timeViewer = timeDataViewers.Key;
                if (timeViewer.TimePoint != datapoint.Time) continue;

                // We already have a time viewer for this datapoint, check if we also have a dataviewer
                foreach (var dataViewer in timeDataViewers.Value)
                {
                    if (dataViewer.DataPoint != datapoint) continue;
                    timeDataViewers.Value.Add(dataViewer);
                    return dataViewer;
                }

                // If not, create one
                var newViewer = await datapoint.CreateViewer();
                timeDataViewers.Value.Add(newViewer);
                SetTimeRangeForViewer(timeViewer, newViewer, SelectedTimeFrom, SelectedTimeTo);
                return newViewer;
            }

            // If we don't have a timeviewer, we need to create both
            var newDataViewer = await datapoint.CreateViewer();
            var newTimeViewer = await datapoint.Time.CreateViewer();
            _viewers.Add(newTimeViewer, new List<IDataViewer> {newDataViewer});
            newTimeViewer.TimeChanged += OnTimeViewerAvailableChanged;
            OnTimeViewerAvailableChanged(newTimeViewer, newTimeViewer.Start, newTimeViewer.End);

            return newDataViewer;
        }

        protected abstract void OnTimeViewerAvailableChanged(ITimeViewer sender, long start, long end);

        // --- Selected time range ---
        private int _lastViewers = -1;


        public event DataViewerContextSelectedRangeChangedHandler SelectedTimeRangeChanged;

        protected void InternalSetSelectedTimeRange(long from, long to)
        {
            var nViewers = _viewers.Count;
            if (SelectedTimeFrom == from && SelectedTimeTo == to && _lastViewers == nViewers) return;

            SelectedTimeFrom = from;
            SelectedTimeTo = to;

            // TODO: Maybe we should set the mapping instead of doing this every time
            foreach (var timeDataViewers in _viewers)
            {
                var timeViewer = timeDataViewers.Key;
                foreach (var dataViewer in timeDataViewers.Value)
                    SetTimeRangeForViewer(timeViewer, dataViewer, from, to);
            }

            SelectedTimeRangeChanged?.Invoke(this, from, to);
            _lastViewers = nViewers;
        }

        // --- Available time range based on current time _viewers ---
        protected (long, long) GetAvailableTimeMinMax(bool useWorldClock)
        {
            if (_viewers.Count == 0)
            {
                return (0L, 0L);
            }

            // If we are using world clock, take the min start and max end. If not, return zero and longest data set.
            return useWorldClock
                ? (_viewers.Min(viewer => viewer.Key.Start), _viewers.Max(viewer => viewer.Key.End))
                : (0L, _viewers.Max(viewer => viewer.Key.End - viewer.Key.Start));
        }

        private void UpdateSetAvailableTimeRange()
        {
            var (min, max) = GetAvailableTimeMinMax(IsSynchronizedToWorldClock);
            InternalSetAvailableTimeRange(min, max);
        }

        public void Remove(IDataViewer dataViewer)
        {
            var toRemove = new List<ITimeViewer>();
            foreach (var item in _viewers)
            {
                item.Value.Remove(dataViewer);
                if (!item.Value.Any())
                {
                    toRemove.Add(item.Key);
                }
            }

            foreach (var viewer in toRemove)
            {
                _viewers.Remove(viewer);
            }

            UpdateSetAvailableTimeRange();
        }
    }
}
