using SINTEF.AutoActive.Databus.Interfaces;
using System.Collections.Generic;

namespace SINTEF.AutoActive.Databus
{
    public delegate void DataProviderAddedHandler(IDataProvider dataprovider);
    public delegate void DataProviderRemovedHandler(IDataProvider dataprovider);

    public static class DataRegistry
    {
        private static readonly List<IDataProvider> dataproviders = new List<IDataProvider>();
        //TODO(sigurdal): I do not understand why this is needed?
        private static readonly List<IDataPoint> datapoints = new List<IDataPoint>();

        private static void OnDataPointAdded(IDataStructure datastructure, IDataPoint datapoint)
        {
            if (datapoints.Contains(datapoint)) return;
            datapoints.Add(datapoint);
            DataPointAdded?.Invoke(null, datapoint);
        }

        private static void OnDataPointRemoved(IDataStructure datastructure, IDataPoint datapoint)
        {
            if (!datapoints.Remove(datapoint)) return;
            DataPointRemoved?.Invoke(null, datapoint);
        }

        private static void InvokeAddDataPointsOnAll(IDataStructure datastructure)
        {
            foreach (var child in datastructure.Children)
            {
                InvokeAddDataPointsOnAll(child);
            }
            foreach (var point in datastructure.DataPoints)
            {
                OnDataPointAdded(datastructure, point);
            }
        }

        private static void InvokeRemoveDataPointsOnAll(IDataStructure datastructure)
        {
            foreach (var child in datastructure.Children)
            {
                InvokeRemoveDataPointsOnAll(child);
            }
            foreach (var point in datastructure.DataPoints)
            {
                OnDataPointRemoved(datastructure, point);
            }
        }

        private static List<IDataStructure> ExtractParents(Dictionary<IDataStructure, IDataStructure> map, IDataStructure el)
        {
            var retList = new List<IDataStructure> {el};

            while (map.TryGetValue(el, out var parent))
            {
                retList.Add(parent);
                el = parent;
            }

            return retList;
        }
        public static List<IDataStructure> GetParents(IDataPoint dataPoint)
        {
            var queue = new Queue<IDataStructure>();
            var parentMap = new Dictionary<IDataStructure, IDataStructure>();
            foreach (var provider in Providers)
            {
                queue.Enqueue(provider);
            }

            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                foreach (var el in parent.DataPoints)
                {
                    if (el == dataPoint)
                    {
                        return ExtractParents(parentMap, parent);
                    }
                }

                foreach (var child in parent.Children)
                {
                    parentMap[child] = parent;
                    queue.Enqueue(child);
                }
            }

            return null;
        }

        /* -- Public API -- */
        public static IReadOnlyCollection<IDataProvider> Providers => dataproviders.AsReadOnly();

        // Global events for the whole registry
        public static event DataProviderAddedHandler ProviderAdded;
        public static event DataProviderRemovedHandler ProviderRemoved;
        public static event DataPointAddedHandler DataPointAdded;
        public static event DataPointRemovedHandler DataPointRemoved;

        // DataProvider extensions
        public static void Register(this IDataProvider dataprovider)
        {
            if (dataproviders.Contains(dataprovider)) return;
            dataproviders.Add(dataprovider);
            dataprovider.DataPointAdded += OnDataPointAdded;
            dataprovider.DataPointRemoved += OnDataPointRemoved;
            ProviderAdded?.Invoke(dataprovider);
            InvokeAddDataPointsOnAll(dataprovider);
        }

        public static void Unregister(this IDataProvider dataprovider)
        {
            if (!dataproviders.Remove(dataprovider)) return;
            dataprovider.DataPointAdded -= OnDataPointAdded;
            dataprovider.DataPointRemoved -= OnDataPointRemoved;
            InvokeRemoveDataPointsOnAll(dataprovider);
            ProviderRemoved?.Invoke(dataprovider);
            dataprovider.Close();
        }

        public static T FindFirstDataStructure<T>(IReadOnlyCollection<IDataProvider> providers)
        {
            var dataStructure = new Queue<IDataProvider>();
            foreach (var provider in providers)
            {
                if (provider is T item)
                    return item;

                dataStructure.Enqueue(provider);
            }

            while (dataStructure.Count > 0)
            {
                var el = dataStructure.Dequeue();
                foreach (var child in el.Children)
                {
                    if (child is T item)
                        return item;

                    if (child is IDataProvider provider)
                    {
                        dataStructure.Enqueue(provider);
                    }
                }


            }
            return default;
        }
    }
}
