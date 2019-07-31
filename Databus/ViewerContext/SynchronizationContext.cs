using SINTEF.AutoActive.Databus.Interfaces;

namespace SINTEF.AutoActive.Databus.ViewerContext
{
    public class SynchronizationContext : TimeSynchronizedContext
    {
        private long _selectedFrom;
        private long _selectedTo;

        private long _offset;
        public long Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                TransformSelectedTime();
            }
        }

        private double _scale = 1;
        public double Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                TransformSelectedTime();
            }
        }

        private long TransformTime(long time)
        {
            return (long)(time * Scale) + _offset;
        }

        private void TransformSelectedTime()
        {
            SetSelectedTimeRange(
                TransformTime(_selectedFrom),
                TransformTime(_selectedTo));
        }

        public override (long, long) GetAvailableTimeInContext(ITimeViewer view)
        {
            return (view.Start - _offset, view.End - _offset);
        }

        public SynchronizationContext(SingleSetDataViewerContext masterContext)
        {
            SetSynchronizedToWorldClock(true);
            masterContext.SelectedTimeRangeChanged +=
                (sender, from, to) =>
                {
                    _selectedFrom = from;
                    _selectedTo = to;
                    TransformSelectedTime();
                };
            masterContext.IsPlayingChanged += (s, playing) => IsPlaying = playing;
            masterContext.PlaybackRateChanged += (s, rate) => PlaybackRate = rate;

            _selectedFrom = masterContext.SelectedTimeFrom;
            _selectedTo = masterContext.SelectedTimeTo;

            IsPlaying = masterContext.IsPlaying;
            PlaybackRate = masterContext.PlaybackRate;
        }

    }
}
