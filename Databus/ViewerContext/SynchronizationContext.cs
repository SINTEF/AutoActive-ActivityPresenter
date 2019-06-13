namespace SINTEF.AutoActive.Databus.ViewerContext
{
    public class SynchronizationContext : TimeSynchronizedContext
    {
        private long _selectedFrom;
        private long _selectedTo;

        private long _availableFrom;
        private long _availableTo;

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
                TransformAvailableTime();
            }
        }

        private long TransformTime(long time)
        {
            return (long)(time * Scale) + _offset;
        }

        private void TransformSelectedTime()
        {
            SetSelectedTimeRange(TransformTime(_selectedFrom), TransformTime(_selectedTo));
        }

        private void TransformAvailableTime()
        {
            InternalSetAvailableTimeRange(TransformTime(_availableFrom), TransformTime(_availableTo));
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
            masterContext.AvailableTimeRangeChanged += (sender, from, to) =>
            {
                _availableFrom = from;
                _availableTo = to;
                TransformAvailableTime();
            };
            masterContext.IsPlayingChanged += (s, playing) => IsPlaying = playing;
            masterContext.PlaybackRateChanged += (s, rate) => PlaybackRate = rate;

            IsPlaying = masterContext.IsPlaying;
            PlaybackRate = masterContext.PlaybackRate;
        }

    }
}
