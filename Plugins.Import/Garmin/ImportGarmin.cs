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
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;

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
        public IEnumerable<Speed> SpeedList { get; set; }
        public IEnumerable<Position> PositionList { get; set; }
    }

    public class Speed
    {
        public double SpeedMS { get; set; }
    }

    public class Position
    {
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

    // TODO: Split each activity and lap into their own datastructures.
    // Also, data should not be loaded until the viewer is created (see ArchiveTable)

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

                IEnumerable<XElement> tp =
                    from el in ae.Descendants(ns1 + "Trackpoint")
                    select el;
                Debug.WriteLine("tp.Count(): " + tp.Count());

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
                        SpeedList = 
                            (from els in el.Descendants(ns3 + "Speed")
                            select new Speed
                            {
                                SpeedMS = els != null ?
                                Convert.ToDouble((string)els.Value ) :
                                0.0,
                            }),
                        PositionList = 
                            (from elp in el.Descendants(ns1 + "Position")
                            select new Position
                            {
                                LatitudeDegrees = elp.Element(ns1 + "LatitudeDegrees") != null ?
                                Convert.ToDouble((string)elp.Element(ns1 + "LatitudeDegrees").Value) :
                                0.0,
                                LongitudeDegrees = elp.Element(ns1 + "LongitudeDegrees") != null ?
                                Convert.ToDouble((string)elp.Element(ns1 + "LongitudeDegrees").Value) :
                                0.0,
                            }),
                    };

                int c5 = trackpoints.Count();
                Debug.WriteLine("trackpoint.Count(): " + trackpoints.Count());

                // The var trackpoints is an LYNQ object with late fetch
                // Make a list of it to force fetch of data into a real list that we can change.
                var trackpointList = trackpoints.ToList();

                // The SpeedList and PositionList will only be populated if data found
                // Loop through the list and copy the values to the main class for consistency
                foreach (var entry in trackpointList)
                {
                    if(entry.PositionList.Count() > 0)
                    {
                        entry.LatitudeDegrees = entry.PositionList.First().LatitudeDegrees;
                        entry.LongitudeDegrees = entry.PositionList.First().LongitudeDegrees;
                    }
                    else
                    {
                        entry.LatitudeDegrees = 0.0;
                        entry.LongitudeDegrees = 0.0;
                    }
                    if (entry.SpeedList.Count() > 0)
                    {
                        entry.SpeedMS = entry.SpeedList.First().SpeedMS;
                    }
                    else
                    {
                        entry.SpeedMS = 0.0;
                    }
                }

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

                AddChild(new GarminTable(id, trackpointList));
            }
        }
    }

    public class GarminTable : BaseDataStructure
    {
        internal GarminTable(string id, List<TrackPoint> tpList)
        {
            Name = id;
            convertTrackpoints(tpList);
        }

        /* Open an existing gamin file */
        private void convertTrackpoints(List<TrackPoint> tpList)
        {
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
            var timeCol = new TableIndex("Time", GenerateLoader(tpList, entry => (float)entry.TimeEpoch / 1000));

            // Add other columns
            this.AddColumn("AltitudeMeters", GenerateLoader(tpList, entry => (float)entry.AltitudeMeters), timeCol);
            this.AddColumn("DistanceMeters", GenerateLoader(tpList, entry => (float)entry.DistanceMeters), timeCol);
            this.AddColumn("SpeedMS", GenerateLoader(tpList, entry => (float)entry.SpeedMS), timeCol);
            this.AddColumn("HeartRateBpm", GenerateLoader(tpList, entry => (float)entry.HeartRateBpm / 200), timeCol);
            this.AddColumn("LatitudeDegrees", GenerateLoader(tpList, entry => (float)entry.LatitudeDegrees), timeCol);
            this.AddColumn("LongitudeDegrees", GenerateLoader(tpList, entry => (float)entry.LongitudeDegrees), timeCol);
        }

        private Task<T[]> GenerateLoader<T>(List<TrackPoint> trackPoints, Func<TrackPoint, T> fetchValue)
        {
            return new Task<T[]>(() =>
            {
                T[] data = new T[trackPoints.Count];
                var i = 0;
                foreach (var trackPoint in trackPoints)
                {
                    data[i++] = fetchValue(trackPoint);
                }
                return data;
            });
        }
    }
}