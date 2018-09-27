
namespace SINTEF.AutoActive.Databus.Interfaces
{
    public interface IDataProvider : IDataStructure
    {
        // An IDataProvider should also emit events from it's entire tree
        event DataStructureAddedHandler DataStructureAddedToTree;
        event DataStructureRemovedHandler DataStructureRemovedFromTree;
        event DataPointAddedHandler DataPointAddedToTree;
        event DataPointRemovedHandler DataPointRemovedFromTree;
    }
}
