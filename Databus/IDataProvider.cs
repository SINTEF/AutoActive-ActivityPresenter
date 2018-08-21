using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Databus
{
    public delegate void DataPointAddedHandler(IDataPoint datapoint);
    public delegate void DataPointAddedToHandler(IDataPoint datapoint, DataStructure parent);
    public delegate void DataPointRemovedHandler(IDataPoint datapoint);

    public delegate void DataStructureAddedHandler(DataStructure datastructure);
    public delegate void DataStructureAddedToHandler(DataStructure datastructure, DataStructure parent);
    public delegate void DataStructureRemovedHandler(DataStructure datastructure);

    public interface IDataProvider
    {
        event DataPointAddedToHandler DataPointAddedTo;
        event DataPointRemovedHandler DataPointRemoved;

        event DataStructureAddedToHandler DataStructureAddedTo;
        event DataStructureRemovedHandler DataStructureRemoved;
    }
}
