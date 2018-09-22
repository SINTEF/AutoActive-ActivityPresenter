
namespace SINTEF.AutoActive.Databus.Interfaces
{
    public delegate void DataViewWasChangedHandler();
    
    public interface IDataViewer
    {
        IDataPoint DataPoint { get; }
        event DataViewWasChangedHandler Changed;
    }
}
