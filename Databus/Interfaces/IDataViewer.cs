
namespace SINTEF.AutoActive.Databus.Interfaces
{
    public delegate void DataViewWasChangedHandler();
    public delegate void DataViewHasDataRangeChangedHandler(double from, double to);
    
    public interface IDataViewer
    {
        IDataPoint DataPoint { get; }

        event DataViewWasChangedHandler Changed;

        double HasDataFrom { get; }
        double HasDataTo { get; }
        event DataViewHasDataRangeChangedHandler HasDataRangeChanged;
    }
}
