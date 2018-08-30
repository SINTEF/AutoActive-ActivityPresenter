using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Databus
{
    public abstract class DataStructure
    {
        public abstract string Name { get; set;  }
        // Path
        // Icon
        // Description
        // Other metadata

        internal readonly List<IDataPoint> _datapoints = new List<IDataPoint>();

        public IReadOnlyList<IDataPoint> Datapoints
        {
            get
            {
                return _datapoints.AsReadOnly();
            }
        }

        internal readonly List<DataStructure> _children = new List<DataStructure>();

        public IReadOnlyList<DataStructure> Children
        {
            get
            {
                return _children.AsReadOnly();
            }
        }

        public event DataStructureAddedHandler DataStructureAdded;
        public event DataStructureRemovedHandler DataStructureRemoved;
        public event DataPointAddedHandler DataPointAdded;
        public event DataPointRemovedHandler DataPointRemoved;

        internal void InvokeDataStructureAdded(DataStructure datastructure)
        {
            DataStructureAdded?.Invoke(datastructure);
        }
        internal void InvokeDataStructureRemoved(DataStructure datastructure)
        {
            DataStructureRemoved?.Invoke(datastructure);
        }
        internal void InvokeDataPointAdded(IDataPoint datapoint)
        {
            DataPointAdded?.Invoke(datapoint);
        }
        internal void InvokeDataPointRemoved(IDataPoint datapoint)
        {
            DataPointRemoved?.Invoke(datapoint);
        }
    }
}
