using SINTEF.AutoActive.Databus.Interfaces;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus
{
    public enum DataViewerRangeType
    {
        Time, Index
    }

    public delegate void DataViewerRangeUpdated(double from, double to);
    public delegate void DataViewerDataRangeUpdated(double from, double to);

    public class DataViewerContext
    {
        public DataViewerContext(DataViewerRangeType range, double from, double to)
        {
            RangeType = range;
            RangeFrom = from;
            RangeTo = to;
        }

        // ---- Range to show data from ----
        public DataViewerRangeType RangeType { get; private set; }

        public double RangeFrom { get; private set; }
        public double RangeTo { get; private set; }

        // Force the whole range to be updated at the same time, to stop double-loading of data
        // It's still possible to supply only one of the values, and leave the other unchanged (using null)
        public void UpdateRange(double? from, double? to)
        {
            if (from.HasValue) RangeFrom = from.Value;
            if (to.HasValue) RangeTo = to.Value;

            RangeUpdated?.Invoke(RangeFrom, RangeTo);
        }

        public event DataViewerRangeUpdated RangeUpdated;

        // ---- Range that we currently have data from -----
        public double HasDataFrom { get; private set; } = 0;
        public double HasDataTo { get; private set; } = 0;

        public event DataViewerDataRangeUpdated DataRangeUpdated;

        private void UpdateDataRangeIncrementally(double from, double to)
        {
            var wasUpdated = false;
            if (from < HasDataFrom)
            {
                HasDataFrom = from;
                wasUpdated = true;
            }
            if (to > HasDataTo)
            {
                HasDataTo = to;
                wasUpdated = true;
            }
            if (wasUpdated) DataRangeUpdated?.Invoke(HasDataFrom, HasDataTo);
        }

         // ---- Data viewers ----
        private Dictionary<IDataPoint, IDataViewer> dataviewers = new Dictionary<IDataPoint, IDataViewer>();

        public async Task<IDataViewer> GetViewerFor(IDataPoint datapoint)
        {
            // Check if there already is a viewer for this datapoint
            if (!dataviewers.TryGetValue(datapoint, out IDataViewer viewer))
            {
                // If not, create one
                viewer = await datapoint.CreateViewerIn(this);
                dataviewers[datapoint] = viewer;
                // Also subscribe to the changes in available data
                viewer.HasDataRangeChanged += UpdateDataRangeIncrementally;
                UpdateDataRangeIncrementally(viewer.HasDataFrom, viewer.HasDataTo);
                Debug.WriteLine($"NEW VIEWER DATA: {viewer.HasDataFrom} -> {viewer.HasDataTo}");
            }
            return viewer;
        }

        // FIXME: Removal of viewers
    }
}
