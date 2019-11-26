
namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface IDataProvider : IDataStructure
    {
        // Close stream and all children and datapoints
        void Close();
    }
}
