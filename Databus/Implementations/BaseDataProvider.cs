using SINTEF.AutoActive.Databus.Interfaces;
using System;

namespace SINTEF.AutoActive.Databus.Implementations
{
    // TODO: Should these methods perhaps be sealed?
    public abstract class BaseDataProvider : BaseDataStructure, IDataProvider
    {
        // DataPoints are not supported
        protected internal override void AddDataPoint(IDataPoint datapoint)
        {
            throw new NotSupportedException("BaseDataProvider cannot contain datapoints");
        }

        protected internal override void RemoveDataPoint(IDataPoint datapoint)
        {
            throw new NotSupportedException("BaseDataProvider cannot contain datapoints");
        }

        // The IDataProvider should also emit events when structures in its tree changes
        protected override void OnChildAdded(IDataStructure sender, IDataStructure datastructure)
        {
            // Emit the original event
            base.OnChildAdded(sender, datastructure);

            // Subscribe to changes down the tree
            datastructure.ChildAdded += OnChildAdded;
            datastructure.ChildRemoved += OnChildRemoved;
            datastructure.DataPointAdded += OnDataPointAdded;
            datastructure.DataPointRemoved += OnDataPointRemoved;

            // Emit events about the already existing tree
            foreach (var child in datastructure.Children)
            {
                OnChildAdded(datastructure, child);
            }
            foreach (var point in datastructure.DataPoints)
            {
                OnDataPointAdded(datastructure, point);
            }
        }

        protected override void OnChildRemoved(IDataStructure sender, IDataStructure datastructure)
        {
            // Emit events bout the existing tree
            foreach (var child in datastructure.Children)
            {
                OnChildRemoved(datastructure, child);
            }
            foreach (var point in datastructure.DataPoints)
            {
                OnDataPointRemoved(datastructure, point);
            }

            // Unsubscribe from the changes down the tree
            datastructure.ChildAdded -= OnChildAdded;
            datastructure.ChildRemoved -= OnChildRemoved;
            datastructure.DataPointAdded -= OnDataPointAdded;
            datastructure.DataPointRemoved -= OnDataPointRemoved;

            // Emit the original event
            base.OnChildRemoved(sender, datastructure);
        }
    }
}
