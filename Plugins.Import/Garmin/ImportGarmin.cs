using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
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
        public String TimeString { set; get; }
        public Int64 TimeEpoch { set; get; }
        public double AltitudeMeters { get; set; }
        public double DistanceMeters { get; set; }
        public double SpeedMS { get; set; }
        public int HeartRateBpm { get; set; }
        public double LatitudeDegrees { set; get; }
        public double LongitudeDegrees { set; get; }
    }

    [ImportPlugin(".tcx")]
    public class GarminImportPlugin : IImportPlugin
    {
        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory)
        {
            var importer = new GarminImporter(readerFactory.Name);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }
    }

    public class GarminImporter : BaseDataProvider
    {
        internal GarminImporter(string name)
        {
            Name = name;
        }

        internal void ParseFile(Stream s)
        {
            XNamespace ns1 = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";
            XNamespace ns3 = "http://www.garmin.com/xmlschemas/ActivityExtension/v2";

            XElement root = XElement.Load(s);
            IEnumerable<XElement> activity =
                from el in root.Descendants(ns1 + "Activity")
                select el;

            int c2 = activity.Count();
            Debug.WriteLine("activity.Count(): " + activity.Count());

            foreach (XElement ae in activity)
            {

                IEnumerable<XElement> aid =
                    from el in ae.Elements(ns1 + "Id")
                    select el;

                string id = "No id";
                if (aid.Count() > 0)
                    id = (string)aid.First();

                IEnumerable<XElement> lap =
                    from el in ae.Descendants(ns1 + "Lap")
                    select el;

                int c3 = lap.Count();
                Debug.WriteLine("lap.Count(): " + lap.Count());

                IEnumerable<TrackPoint> trackpoints =
                    from el in ae.Descendants(ns1 + "Trackpoint")
                    select new TrackPoint
                    {
                        TimeString = el.Element(ns1 + "Time") != null ?
                        (string)el.Element(ns1 + "Time").Value :
                        "No time",
                        AltitudeMeters = el.Element(ns1 + "AltitudeMeters") != null ?
                        Convert.ToDouble((string)el.Element(ns1 + "AltitudeMeters").Value) :
                        0.0,
                        DistanceMeters = el.Element(ns1 + "DistanceMeters") != null ?
                        Convert.ToDouble((string)el.Element(ns1 + "DistanceMeters").Value) :
                        0.0,
                        HeartRateBpm = el.Element(ns1 + "HeartRateBpm") != null ?
                        Convert.ToInt16((string)el.Element(ns1 + "HeartRateBpm").Value) :
                        0,
                        SpeedMS = el.Descendants(ns3 + "Speed").First() != null ?
                        Convert.ToDouble((string)el.Descendants(ns3 + "Speed").First().Value) :
                        0.0,
                        LatitudeDegrees = el.Element(ns1 + "Position").Element(ns1 + "LatitudeDegrees") != null ?
                        Convert.ToDouble((string)el.Element(ns1 + "Position").Element(ns1 + "LatitudeDegrees").Value) :
                        0.0,
                        LongitudeDegrees = el.Element(ns1 + "Position").Element(ns1 + "LongitudeDegrees") != null ?
                        Convert.ToDouble((string)el.Element(ns1 + "Position").Element(ns1 + "LongitudeDegrees").Value) :
                        0.0,
                    };

                int c5 = trackpoints.Count();
                Debug.WriteLine("trackpoint.Count(): " + trackpoints.Count());

                // foreach (TrackPoint tp in trackpoint)
                // {
                // Debug.WriteLine("tp.TimeString(): " + tp.TimeString);
                // Debug.WriteLine("tp.AltitudeMeters(): " + tp.AltitudeMeters);
                // Debug.WriteLine("tp.DistanceMeters(): " + tp.DistanceMeters);
                // Debug.WriteLine("tp.HeartRateBpm(): " + tp.HeartRateBpm);
                // Debug.WriteLine("tp.SpeedMS(): " + tp.SpeedMS);
                // Debug.WriteLine("tp.LatitudeDegrees(): " + tp.LatitudeDegrees);
                // Debug.WriteLine("tp.LongitudeDegrees(): " + tp.LongitudeDegrees);
                // }
                
                AddChild(new GarminTable(id, trackpoints));
            }
        }
    }

    public class GarminTable : BaseDataStructure
    {
        internal GarminTable(string id, IEnumerable<TrackPoint> tpEnum)
        {
            Name = id;
            convertTrackpoints(tpEnum);
        }

        /* Open an existing gamin file */
        private void convertTrackpoints(IEnumerable<TrackPoint> tpEnum)
        {
            var tpList = tpEnum.ToList();
            var faultyEntries = new List<TrackPoint>();

            // Convert timeString to Epoch
            CultureInfo enUS = new CultureInfo("en-US");
            string[] dateFormatArr = { "yyyy-MM-ddTHH:mm:ss.fffK", "yyyy-MM-ddTHH:mm:ssK" };
            foreach (TrackPoint entry in tpList)
            {

                DateTime dt;
                if (DateTime.TryParseExact(entry.TimeString, dateFormatArr, enUS,
                                 DateTimeStyles.None, out dt))
                {
                    var dto = new DateTimeOffset(dt);
                    entry.TimeEpoch = dto.ToUnixTimeMilliseconds();
                }
                else
                {
                    faultyEntries.Add(entry);
                }

            }

            // Find time duplicates
            Int64 lastTime = 0;
            for (int i = 0; i < tpList.Count(); i++)
            {
                var entry = tpList[i];
                if (lastTime == entry.TimeEpoch)
                    faultyEntries.Add(entry);
                lastTime = entry.TimeEpoch;
            }

            // Remove faulty entries
            foreach (TrackPoint entry in faultyEntries)
                tpList.Remove(entry);

            // Adjust time from EPOCH to start of activity
            Int64 firstTime = tpList[0].TimeEpoch;
            foreach (TrackPoint entry in tpList)
                entry.TimeEpoch = entry.TimeEpoch - firstTime;


            // Create the time index
            var timeCol = new GarminTableIndex("Time", tpList, entry => (float)entry.TimeEpoch / 1000);


            // tcxNames = { 'dist m','altitude m','latitude deg','longitude deg','HR bpm','speed m/s'};
            AddDataPoint(new GarminTableColumn("AltitudeMeters", tpList, entry => (float)entry.AltitudeMeters, timeCol));
            AddDataPoint(new GarminTableColumn("DistanceMeters", tpList, entry => (float)entry.DistanceMeters, timeCol));
            AddDataPoint(new GarminTableColumn("SpeedMS", tpList, entry => (float)entry.SpeedMS, timeCol));
            AddDataPoint(new GarminTableColumn("HeartRateBpm", tpList, entry => (float)entry.HeartRateBpm, timeCol));
            AddDataPoint(new GarminTableColumn("LatitudeDegrees", tpList, entry => (float)entry.LatitudeDegrees, timeCol));
            AddDataPoint(new GarminTableColumn("AltitudeMeters", tpList, entry => (float)entry.AltitudeMeters, timeCol));
            AddDataPoint(new GarminTableColumn("LongitudeDegrees", tpList, entry => (float)entry.LongitudeDegrees, timeCol));
        }
    }


    public class GarminTableColumn : IDataPoint
    {
        protected float[] data;
        GarminTableIndex index;
        SemaphoreSlim locker = new SemaphoreSlim(1, 1);

        internal GarminTableColumn(string name, List<TrackPoint> tpList, Func<TrackPoint, float> fetchValue, GarminTableIndex index)
        {
            Name = name;
            this.index = index;
            Array.Resize(ref data, tpList.Count);
            for (int i = 0; i < tpList.Count; i++)
                data[i] = fetchValue(tpList[i]);
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
        internal GarminTableIndex(string name, List<TrackPoint> tpList, Func<TrackPoint, float> fetchValue)
            : base(name, tpList, fetchValue, null)
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