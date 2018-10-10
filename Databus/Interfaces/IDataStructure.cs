using System.Collections.Generic;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public delegate void DataPointAddedHandler(IDataStructure sender, IDataPoint datapoint);
    public delegate void DataPointRemovedHandler(IDataStructure sender, IDataPoint datapoint);

    public delegate void DataStructureAddedHandler(IDataStructure sender, IDataStructure datastructure);
    public delegate void DataStructureRemovedHandler(IDataStructure sender, IDataStructure datastructure);

    public interface IDataStructure
    {
        string Name { get; }

        // Path
        // Icon
        // Other metadata

        IEnumerable<IDataStructure> Children { get; }
        event DataStructureAddedHandler ChildAdded;
        event DataStructureRemovedHandler ChildRemoved;

        IEnumerable<IDataPoint> DataPoints { get; }
        event DataPointAddedHandler DataPointAdded;
        event DataPointRemovedHandler DataPointRemoved;
    }
}
