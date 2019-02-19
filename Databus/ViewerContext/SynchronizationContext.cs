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
            SetSelectedTimeRange(TransformTime(_selectedFrom), TransformTime(_selectedTo));
        }

        public SynchronizationContext(TimeSynchronizedContext masterContext)
        {
            SetSynchronizedToWorldClock(true);
            masterContext.SelectedTimeRangeChanged +=
                (sender, from, to) =>
                {
                    _selectedFrom = from;
                    _selectedTo = to;
                    TransformSelectedTime();
                };
        }

    }
}
