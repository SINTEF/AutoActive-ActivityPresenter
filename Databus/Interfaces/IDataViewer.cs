
namespace SINTEF.AutoActive.Databus.Interfaces
{
    public delegate void DataViewerWasChangedHandler(IDataViewer sender);
    
    public interface IDataViewer
    {
        IDataPoint DataPoint { get; }

        long CurrentTimeRangeFrom { get; }
        long CurrentTimeRangeTo { get; }

        void SetTimeRange(long from, long to);

        event DataViewerWasChangedHandler Changed;
    }
}
