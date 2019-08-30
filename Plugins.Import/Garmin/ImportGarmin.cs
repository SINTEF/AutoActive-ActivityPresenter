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
        public string TimeString { set; get; }
        public long TimeWorldClock { set; get; }
        public double AltitudeMeters { get; set; }
        public double DistanceMeters { get; set; }
        public double SpeedMS { get; set; }
        public byte HeartRateBpm { get; set; }
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
            importer.ParseFile(await readerFactory.GetReadStream(), readerFactory);
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


        protected override void DoParseFile(Stream s, IReadSeekStreamFactory readerFactory)
        {
            XNamespace ns1 = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";
            XNamespace ns3 = "http://www.garmin.com/xmlschemas/ActivityExtension/v2";

            XElement root = XElement.Load(s);

            var activity = from el in root.Descendants(ns1 + "Activity") select el;
            Debug.WriteLine($"activity.Count(): {activity.Count()}");

            foreach (var ae in activity)
            {

                var aid = from el in ae.Elements(ns1 + "Id") select el;

                var id = aid.Count() > 0 ? (string)aid.First() : "No id";

                var lap = from el in ae.Descendants(ns1 + "Lap") select el;
                Debug.WriteLine($"lap.Count(): {lap.Count()}");

                var tp = from el in ae.Descendants(ns1 + "Trackpoint") select el;
                Debug.WriteLine($"tp.Count(): {tp.Count()}");

                var trackpoints = from el in ae.Descendants(ns1 + "Trackpoint")
                                  select new TrackPoint
                                  {
                                      TimeString = el.Element(ns1 + "Time")?.Value ?? "No time",
                                      AltitudeMeters = Convert.ToDouble(el.Element(ns1 + "AltitudeMeters")?.Value),
                                      DistanceMeters = Convert.ToDouble(el.Element(ns1 + "DistanceMeters")?.Value),
                                      HeartRateBpm = Convert.ToByte(el.Element(ns1 + "HeartRateBpm")?.Value),
                                      SpeedList = from els in el.Descendants(ns3 + "Speed") select new Speed { SpeedMS = Convert.ToDouble(els.Value) },
                                      PositionList = from elp in el.Descendants(ns1+"Position")
                                                   select new Position
                                                   {
                                                        LatitudeDegrees = Convert.ToDouble(elp.Element(ns1 + "LongitudeDegrees")?.Value),
                                                        LongitudeDegrees = Convert.ToDouble(elp.Element(ns1 + "LongitudeDegrees")?.Value),
                                                   },

                                  };

                Debug.WriteLine($"trackpoint.Count(): {trackpoints.Count()}");

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
            ConvertTrackpoints(tpList);
        }

        /* Open an existing gamin file */
        private void ConvertTrackpoints(List<TrackPoint> tpList)
        {
            var faultyEntries = new List<TrackPoint>();

            // Convert timeString to Epoch
            CultureInfo enUS = new CultureInfo("en-US");
            string[] dateFormatArr = { "yyyy-MM-ddTHH:mm:ss.fffK", "yyyy-MM-ddTHH:mm:ssK" };
            foreach (TrackPoint entry in tpList)
            {

                if (DateTime.TryParseExact(entry.TimeString, dateFormatArr, enUS, DateTimeStyles.None, out var dt))
                {
                    entry.TimeWorldClock = dt.Ticks / 10;
                }
                else
                {
                    faultyEntries.Add(entry);
                }

            }

            // Find time duplicates
            long lastTime = 0;
            for (int i = 0; i < tpList.Count(); i++)
            {
                var entry = tpList[i];
                if (lastTime == entry.TimeWorldClock)
                    faultyEntries.Add(entry);
                lastTime = entry.TimeWorldClock;
            }

            // Remove faulty entries
            foreach (TrackPoint entry in faultyEntries)
                tpList.Remove(entry);


            var uri = "LIVE";
            // Create the time index
            var timeCol = new TableTimeIndex("Time", GenerateLoader(tpList, entry => entry.TimeWorldClock), true, uri);

            // Add other columns
            this.AddColumn("AltitudeMeters", GenerateLoader(tpList, entry => entry.AltitudeMeters), timeCol, uri);
            this.AddColumn("DistanceMeters", GenerateLoader(tpList, entry => entry.DistanceMeters), timeCol, uri);
            this.AddColumn("SpeedMS", GenerateLoader(tpList, entry => entry.SpeedMS), timeCol, uri);
            this.AddColumn("HeartRateBpm", GenerateLoader(tpList, entry => entry.HeartRateBpm), timeCol, uri);
            this.AddColumn("LatitudeDegrees", GenerateLoader(tpList, entry => entry.LatitudeDegrees), timeCol, uri);
            this.AddColumn("LongitudeDegrees", GenerateLoader(tpList, entry => entry.LongitudeDegrees), timeCol, uri);
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