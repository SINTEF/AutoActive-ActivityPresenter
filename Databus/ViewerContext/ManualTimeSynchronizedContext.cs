namespace SINTEF.AutoActive.Databus.ViewerContext
{
    public class ManualTimeSynchronizedContext : TimeSynchronizedContext
    {
        public override long AvailableTimeFrom { get; protected set; }
        public override long AvailableTimeTo { get; protected set; }

        public void SetAvailableTime(long from, long to)
        {
            AvailableTimeFrom = from;
            AvailableTimeTo = to;
        }
    }
}
