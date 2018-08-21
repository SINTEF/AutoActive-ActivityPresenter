using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Databus
{
    public class RootDataStructure : DataStructure
    {
        internal RootDataStructure()
        {

        }
    }

    public static class DataRegistry
    {
        private static readonly DataStructure root = new RootDataStructure();
        private static readonly List<IDataPoint> datapoints = new List<IDataPoint>();

        public static DataStructure RootStructure
        {
            get => root;
        }

        public static IReadOnlyList<IDataPoint> DataPoints
        {
            get => datapoints.AsReadOnly();
        }

        /* --- Global events --- */
        public static event DataStructureAddedHandler DataStructureAdded;
        public static event DataStructureRemovedHandler DataStructureRemoved;
        public static event DataPointAddedHandler DataPointAdded;
        public static event DataPointRemovedHandler DataPointRemoved;


        /* --- Tree traversing helpers --- */
        private static DataStructure SearchForDataStructure(DataStructure target, DataStructure parent)
        {
            foreach (DataStructure structure in parent._children)
            {
                if (structure == target) return target;
                var recursive = SearchForDataStructure(target, structure);
                if (recursive != null) return recursive;
            }
            return null;
        }

        private static DataStructure SearchForDataPoint(IDataPoint target, DataStructure parent)
        {
            if (parent._datapoints.Contains(target)) return parent;
            foreach (DataStructure structure in parent._children)
            {
                var recursive = SearchForDataPoint(target, structure);
                if (recursive != null) return recursive;
            }
            return null;
        }
        

        /* --- DataProvider extension --- */
        public static void Register(this IDataProvider provider)
        {
            provider.DataStructureAddedTo += (DataStructure datastructure, DataStructure parent) =>
            {
                var wasAdded = false;
                lock (root)
                {
                    // Make sure the parent is in our tree
                    if (SearchForDataStructure(parent, root) != null)
                    {
                        parent._children.Add(datastructure);
                        wasAdded = true;
                    }
                }
                if (wasAdded)
                {
                    DataStructureAdded?.Invoke(datastructure);
                    parent.InvokeDataStructureAdded(datastructure);
                }
            };

            provider.DataPointAddedTo += (IDataPoint datapoint, DataStructure parent) =>
            {
                var wasAdded = false;
                lock (root)
                {
                    // Make sure the parent is in our tree
                    if (SearchForDataStructure(parent, root) != null)
                    {
                        parent._datapoints.Add(datapoint);
                        datapoints.Add(datapoint);
                        wasAdded = true;
                    }
                }
                if (wasAdded)
                {
                    DataPointAdded?.Invoke(datapoint);
                    parent.InvokeDataPointAdded(datapoint);
                }
            };

            provider.DataStructureRemoved += (DataStructure datastructure) =>
            {
                var wasRemoved = false;
                DataStructure parent;
                lock (root)
                {
                    // Find the parent in the tree
                    parent = SearchForDataStructure(datastructure, root);
                    if (parent != null)
                    {
                        parent._children.Remove(datastructure);
                        wasRemoved = true;
                    }
                }
                if (wasRemoved)
                {
                    DataStructureRemoved?.Invoke(datastructure);
                    parent.InvokeDataStructureRemoved(datastructure);
                }
            };

            provider.DataPointRemoved += (IDataPoint datapoint) =>
            {
                var wasRemoved = false;
                DataStructure parent;
                lock (root)
                {
                    // Find the parent in the tree
                    parent = SearchForDataPoint(datapoint, root);
                    if (parent != null)
                    {
                        parent._datapoints.Remove(datapoint);
                        datapoints.Remove(datapoint);
                        wasRemoved = true;
                    }
                }
                if (wasRemoved)
                {
                    DataPointRemoved?.Invoke(datapoint);
                    parent.InvokeDataPointRemoved(datapoint);
                }
            };
        }

    }
}
