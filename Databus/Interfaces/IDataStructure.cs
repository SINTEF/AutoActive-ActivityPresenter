using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SINTEF.AutoActive.Databus.Interfaces
{
    public delegate void DataPointAddedHandler(IDataStructure sender, IDataPoint datapoint);
    public delegate void DataPointRemovedHandler(IDataStructure sender, IDataPoint datapoint);

    public delegate void DataStructureAddedHandler(IDataStructure sender, IDataStructure datastructure);
    public delegate void DataStructureRemovedHandler(IDataStructure sender, IDataStructure datastructure);

    public interface IDataStructure
    {
        string Name { get; set; }

        // Path
        // Icon
        // Other metadata

        // Close all children and datapoints
        void Close();

        ObservableCollection<IDataStructure> Children { get; }
        //TODO: remove these events and use ObservableCollection's events
        event DataStructureAddedHandler ChildAdded;
        event DataStructureRemovedHandler ChildRemoved;
        void AddChild(IDataStructure dataStructure);
        void RemoveChild(IDataStructure dataStructure);

        ObservableCollection<IDataPoint> DataPoints { get; }
        //TODO: remove these events and use ObservableCollection's events
        event DataPointAddedHandler DataPointAdded;
        event DataPointRemovedHandler DataPointRemoved;
        void AddDataPoint(IDataPoint dataPoint);
        void RemoveDataPoint(IDataPoint dataPoint);
    }
}
