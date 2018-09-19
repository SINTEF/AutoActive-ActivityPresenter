using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.Import.Garmin
{

    public class TrackPoint
    {
        public string Time { set; get; }
        public double AltitudeMeters { get; set; }
        public double DistanceMeters { get; set; }
        public int HeartRateBpm { get; set; }
        public int Cadence { get; set; }
        public string SensorState { get; set; }
        //public List<Position> Positionx { get; set; }
        public double LatitudeDegrees { set; get; }
        public double LongitudeDegrees { set; get; }
    }

    public class Position
    {
        public double LatitudeDegrees { set; get; }
        public double LongitudeDegrees { set; get; }
    }


    public class GarminImporter : IDataProvider
    {
        public event DataPointAddedToHandler DataPointAddedTo;
        public event DataPointRemovedHandler DataPointRemoved;
        public event DataStructureAddedToHandler DataStructureAddedTo;
        public event DataStructureRemovedHandler DataStructureRemoved;

        public GarminImporter()
        {
        }

        /* Open an existing gamin file */
        public async static void Open(IReadSeekStreamFactory file)
        {
            XElement root = XElement.Load(await file.GetReadStream());
            XNamespace ns1 = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";

            IEnumerable<XElement> lap =
                from el in root.Descendants(ns1 + "Lap")
                select el;

            int c3 = lap.Count();
            Trace.WriteLine("lap.Count(): " + lap.Count());


            int lapCount = 0;
            int trackpointCount = 0;
            foreach (XElement lapEl in lap)
            {
                Trace.WriteLine("Lap num: " + lapCount++);

                IEnumerable<TrackPoint> trackpoint =
                    from el in lapEl.Descendants(ns1 + "Trackpoint")
                    select new TrackPoint
                    {
                        Time = el.Element(ns1 + "Time") != null ?
                        (string)el.Element(ns1 + "Time").Value :
                        "No time",

                        HeartRateBpm = el.Element(ns1 + "HeartRateBpm") != null ?
                        Convert.ToInt16((string) el.Element(ns1 + "HeartRateBpm").Value) :
                        0,
                        LatitudeDegrees = el.Element(ns1 + "Position").Element(ns1 + "LatitudeDegrees") != null ?
                        Convert.ToDouble((string)el.Element(ns1 + "Position").Element(ns1 + "LatitudeDegrees").Value) :
                        0.0,
                        LongitudeDegrees = el.Element(ns1 + "Position").Element(ns1 + "LongitudeDegrees") != null ?
                        Convert.ToDouble((string)el.Element(ns1 + "Position").Element(ns1 + "LongitudeDegrees").Value) :
                        0.0,
                    };

                int c5 = trackpoint.Count();
                trackpointCount += c5;
                Trace.WriteLine("trackpoint.Count(): " + trackpoint.Count());
                foreach (TrackPoint tp in trackpoint)
                {
                    Trace.WriteLine("tp.Time(): " + tp.Time);
                    Trace.WriteLine("tp.HeartRateBpm(): " + tp.HeartRateBpm);
                    Trace.WriteLine("tp.LatitudeDegrees(): " + tp.LatitudeDegrees);
                    Trace.WriteLine("tp.LongitudeDegrees(): " + tp.LongitudeDegrees);

                }

            }
            Trace.WriteLine("Total trackpoints: " + trackpointCount++);



            // IEnumerable<XElement> activities = from ae in root.Descendants(ns1 + "Activities")
            //                                   select ae;
            // foreach (var ae in activities)
            //    Trace.WriteLine(ae);

            // Check data

            // TODO: Multiple indices
            //var timeInd = Array.IndexOf<string>(columns, "Time");
            //if (timeInd < 0) throw new ArgumentException("Table does not have a column named 'Time'");

            // Create the time index
            //var timeCol = new GarminTableIndex("Time", zipEntry, archive);

            // Create the other columns
            //for (var i = 0; i < columns.Length; i++)
            //{
            //    if (i != timeInd)
            //    {
            //        this.columns.Add(new GarminTableColumn(columns[i], zipEntry, archive, timeCol));
            //    }
            //}

        }

    }

    public class GarminTable : DataStructure
    {
        public override string Name { get; set; }

        private readonly List<GarminTableColumn> columns = new List<GarminTableColumn>();

        /* Open an existing gamin file */
        internal GarminTable()
        {
            // Import data

            // Check data

            // Create index

            // Create the time index
            var timeCol = new GarminTableIndex("Time");

            // Create the other columns
            for (var i = 0; i < 1; i++)
            {
                this.columns.Add(new GarminTableColumn("Name", timeCol));
            }

        }

    }

    public class GarminTableColumn : IDataPoint
    {
        protected float[] data;
        GarminTableIndex index;
        SemaphoreSlim locker = new SemaphoreSlim(1, 1);

        internal GarminTableColumn(string name, GarminTableIndex index)
        {
            Name = name;
            this.index = index;
        }

        public Span<float> GetData(int start, int end)
        {
            return data.AsSpan(start, end - start);
        }

        public Type Type => throw new NotImplementedException();

        public string Name { get; set; }

        public async Task<IDataViewer> CreateViewerIn(DataViewerContext context)
        {
            return new GarminTableColumnViewer(index, this, context);
        }
    }

    public class GarminTableIndex : GarminTableColumn
    {
        internal GarminTableIndex(string name )
            : base(name, null)
        {

        }

        internal int findIndex(int current, double value)
        {
            // FIXME: This is far from perfect
            if (current >= 0 && data[current] == value) return current;

            // Do a binary search starting at the previous index
            int first = 0;
            int last = data.Length - 1;

            if (current < 0) current = (first + last) / 2;

            while (first < last)
            {
                if (value < data[first]) return first;
                if (value > data[last]) return last;

                if (value > data[current]) first = current + 1;
                else last = current - 1;
                current = (last + first) / 2;

            }
            return current;
        }
    }

    public class GarminTableColumnViewer : IDataViewer
    {
        GarminTableIndex time;
        GarminTableColumn column;
        DataViewerContext context;
        int startIndex = -1;
        int endIndex = -1;

        internal GarminTableColumnViewer(GarminTableIndex time, GarminTableColumn column, DataViewerContext context)
        {
            this.time = time;
            this.column = column;
            this.context = context;
            context.RangeUpdated += Context_RangeUpdated;
            Context_RangeUpdated(context.RangeFrom, context.RangeTo);
        }

        private void Context_RangeUpdated(double from, double to)
        {
            var start = time.findIndex(startIndex, from);
            var end = time.findIndex(endIndex, to);
            if (start != startIndex || end != endIndex)
            {
                startIndex = start;
                endIndex = end;
                Changed?.Invoke();
            }
        }

        public IDataPoint DataPoint { get; }

        public event DataViewWasChangedHandler Changed;

        public SpanPair<float> GetCurrentFloat()
        {
            return new SpanPair<float>(time.GetData(startIndex, endIndex), column.GetData(startIndex, endIndex));
        }

        public Span<byte> GetCurrentData()
        {
            throw new NotImplementedException();
        }
    }

}
