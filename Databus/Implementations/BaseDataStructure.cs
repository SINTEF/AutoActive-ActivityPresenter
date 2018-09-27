using SINTEF.AutoActive.Databus.Interfaces;
using System.Collections.Generic;

namespace SINTEF.AutoActive.Databus.Implementations
{
    // Base class that implements the IDataStructure interface with lists of children and datapoints
    public abstract class BaseDataStructure : IDataStructure
    {
        private readonly List<IDataStructure> children = new List<IDataStructure>();
        private readonly List<IDataPoint> datapoints = new List<IDataPoint>();

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
        protected internal virtual void AddChild(IDataStructure datastructure)
        {
            if (Contains(datastructure)) return;
            children.Add(datastructure);
            OnChildAdded(this, datastructure);
        }
        protected virtual void OnChildAdded(IDataStructure sender, IDataStructure datastructure)
        {
            ChildAdded?.Invoke(sender, datastructure);
        }

        protected internal virtual void RemoveChild(IDataStructure datastructure)
        {
            if (children.Remove(datastructure)) OnChildRemoved(this, datastructure);
        }
        protected virtual void OnChildRemoved(IDataStructure sender, IDataStructure datastructure)
        {
            ChildRemoved?.Invoke(sender, datastructure);
        }

        protected internal virtual void AddDataPoint(IDataPoint datapoint)
        {
            if (Contains(datapoint)) return;
            datapoints.Add(datapoint);
            OnDataPointAdded(this, datapoint);
        }
        protected virtual void OnDataPointAdded(IDataStructure sender, IDataPoint datapoint)
        {
            DataPointAdded?.Invoke(sender, datapoint);
        }

        protected internal virtual void RemoveDataPoint(IDataPoint datapoint)
        {
            if (datapoints.Remove(datapoint)) OnDataPointRemoved(this, datapoint);
        }
        protected virtual void OnDataPointRemoved(IDataStructure sender, IDataPoint datapoint)
        {
            DataPointRemoved?.Invoke(sender, datapoint);
        }

        /* -- Public API -- */
        public virtual string Name { get; protected set; }

        public IEnumerable<IDataStructure> Children => children.AsReadOnly();

        public IEnumerable<IDataPoint> DataPoints => datapoints.AsReadOnly();

        public event DataStructureAddedHandler ChildAdded;
        public event DataStructureRemovedHandler ChildRemoved;
        public event DataPointAddedHandler DataPointAdded;
        public event DataPointRemovedHandler DataPointRemoved;
    }
}
