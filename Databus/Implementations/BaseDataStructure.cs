using SINTEF.AutoActive.Databus.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SINTEF.AutoActive.Databus.Implementations
{
    // Base class that implements the IDataStructure interface with lists of children and datapoints
    public abstract class BaseDataStructure : IDataStructure
    {
        private readonly ObservableCollection<IDataStructure> children = new ObservableCollection<IDataStructure>();
        private readonly ObservableCollection<IDataPoint> datapoints = new ObservableCollection<IDataPoint>();

        // Recursive searches to try to prevent creating loops in the tree of data
        private bool Contains(IDataStructure datastructure)
        {
            foreach (var child in children)
            {
                if (child == datastructure) return true;
                if (child is BaseDataStructure childbase && childbase.Contains(datastructure)) return true;
            }
            return false;
        }
        private bool Contains(IDataPoint datapoint)
        {
            if (datapoints.Contains(datapoint)) return true;
            foreach (var child in children)
            {
                if (child is BaseDataStructure childbase && childbase.Contains(datapoint)) return true;
            }
            return false;
        }

        // Tree manipulation methods
        public virtual void AddChild(IDataStructure datastructure)
        {
            if (Contains(datastructure)) return;
            children.Add(datastructure);
            OnChildAdded(this, datastructure);
        }
        protected virtual void OnChildAdded(IDataStructure sender, IDataStructure datastructure)
        {
            ChildAdded?.Invoke(sender, datastructure);
        }

        public virtual void RemoveChild(IDataStructure datastructure)
        {
            if (children.Remove(datastructure)) OnChildRemoved(this, datastructure);
        }
        protected virtual void OnChildRemoved(IDataStructure sender, IDataStructure datastructure)
        {
            ChildRemoved?.Invoke(sender, datastructure);
        }

        public virtual void AddDataPoint(IDataPoint datapoint)
        {
            if (Contains(datapoint)) return;
            datapoints.Add(datapoint);
            OnDataPointAdded(this, datapoint);
        }
        protected virtual void OnDataPointAdded(IDataStructure sender, IDataPoint datapoint)
        {
            DataPointAdded?.Invoke(sender, datapoint);
        }

        public virtual void RemoveDataPoint(IDataPoint datapoint)
        {
            if (datapoints.Remove(datapoint)) OnDataPointRemoved(this, datapoint);
        }
        protected virtual void OnDataPointRemoved(IDataStructure sender, IDataPoint datapoint)
        {
            DataPointRemoved?.Invoke(sender, datapoint);
        }

        /* -- Public API -- */

        // Remove all children and datapoints
        public virtual void Close()
        {
            while (children.Count > 0)
            {
                RemoveChild(children[0]);
            }
            while (datapoints.Count > 0)
            {
                RemoveDataPoint(datapoints[0]);
            }
        }

        //TODO(sigurdal): make sure name is updated when this is changed
        public virtual string Name { get; set; }

        public ObservableCollection<IDataStructure> Children => children;

        public ObservableCollection<IDataPoint> DataPoints => datapoints;

        public event DataStructureAddedHandler ChildAdded;
        public event DataStructureRemovedHandler ChildRemoved;
        public event DataPointAddedHandler DataPointAdded;
        public event DataPointRemovedHandler DataPointRemoved;
    }
}
