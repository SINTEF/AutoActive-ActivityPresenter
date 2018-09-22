using SINTEF.AutoActive.Databus.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus
{
    public enum DataViewerRangeType
    {
        Time, Index
    }

    public delegate void DataViewerRangeUpdated(double from, double to);

    public class DataViewerContext
    {
        public DataViewerContext(DataViewerRangeType range, double from, double to)
        {
            RangeType = range;
            RangeFrom = from;
            RangeTo = to;
        }

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

        private Dictionary<IDataPoint, IDataViewer> dataviewers = new Dictionary<IDataPoint, IDataViewer>();

        public async Task<IDataViewer> GetViewerFor(IDataPoint datapoint)
        {
            // Check if there already is a viewer for this datapoint
            if (!dataviewers.TryGetValue(datapoint, out IDataViewer viewer))
            {
                // If not, create one
                viewer = await datapoint.CreateViewerIn(this);
                dataviewers[datapoint] = viewer;
            }
            return viewer;
        }

    }
}
