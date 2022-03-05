
namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface IDataViewer
    {
        IDataPoint DataPoint { get; }

        long CurrentTimeRangeFrom { get; }
        long CurrentTimeRangeTo { get; }

        long PreviewPercentage { get; set; }
        void SetTimeRange(long from, long to);
    }
}
