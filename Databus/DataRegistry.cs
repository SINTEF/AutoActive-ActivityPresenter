using SINTEF.AutoActive.Databus.Interfaces;
using System.Collections.Generic;

namespace SINTEF.AutoActive.Databus
{
    public delegate void DataProviderAddedHandler(IDataProvider dataprovider);
    public delegate void DataProviderRemovedHandler(IDataProvider dataprovider);

    public static class DataRegistry
    {
        private static readonly List<IDataProvider> dataproviders = new List<IDataProvider>();
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
        }
    }
}
