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
using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.Plugins.Import.Garmin
{

    [ImportPlugin(".tcx")]
    public class GarminImportPlugin : IImportPlugin
    {
        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory, Dictionary<string, object> parameters)
        {
            var importer = new GarminImporter(readerFactory);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }

        public void GetExtraConfigurationParameters(Dictionary<string, (object, string)> parameters)
        {}

        public Task<bool> CanParse(IReadSeekStreamFactory readerFactory)
        {
            return Task.FromResult(true);
        }
    }

    // TODO: Split each activity and lap into their own datastructures.
    // Also, data should not be loaded until the viewer is created (see ArchiveTable)

    public class GarminImporter : BaseDataProvider
    {
        internal IReadSeekStreamFactory _readerFactory;

        internal GarminImporter(IReadSeekStreamFactory readerFactory)
        {
            Name = readerFactory.Name;
            _readerFactory = readerFactory;
        }

        protected override void DoParseFile(Stream s)
        {
            AddChild(new GarminTable(Name + "_table", _readerFactory, Name + _readerFactory.Extension));
        }

    }


    public class GarminTable : ImportTableBase, ISaveable
    {
        public bool IsSaved { get; }
        private IReadSeekStreamFactory _readerFactory;
        private string _fileName;
        internal GarminTable(string name, IReadSeekStreamFactory readerFactory, string fileName)
        {
            Name = name;
            _readerFactory = readerFactory;
            IsSaved = false;
            _fileName = fileName;

            bool isWorldSynchronized = true;

            var timeColInfo = new ColInfo("time", "us");
            string uri = Name + "/" + timeColInfo.Name;

            _timeIndex = new TableTimeIndex(timeColInfo.Name, GenerateLoader<long>(timeColInfo), isWorldSynchronized, uri, timeColInfo.Unit);

            var stringUnits = new[]
            {
                new ColInfo("altitude", "m"),
                new ColInfo("dist", "m"),
                new ColInfo("speed", "ms"),
                new ColInfo("HR", "bpm"),
                new ColInfo("latitude", "deg"),
                new ColInfo("longitude", "deg"),
            };

            foreach (var colInfo in stringUnits)
            {
                uri = Name + "/" + colInfo.Name;
                this.AddColumn(colInfo.Name, GenerateLoader<double>(colInfo), _timeIndex, uri, colInfo.Unit);
            }


        }

        public override Dictionary<string, Array> ReadData()
        {
            GarminParser gp = new GarminParser(_readerFactory);
            return gp.ReadData();
        }

        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {

            string fileId;

            // TODO: give a better name?
            fileId = "/Import" + "/" + Name + "." + Guid.NewGuid();

            // Make table object
            var metaTable = new JObject { ["type"] = "no.sintef.table" };
            metaTable["attachments"] = new JArray(new object[] { fileId });
            metaTable["units"] = new JArray(GetUnitArray());
            metaTable["is_world_clock"] = _timeIndex.IsSynchronizedToWorldClock;
            metaTable["version"] = 1;

            var userTable = new JObject { };

            var rootTable = new JObject { ["meta"] = metaTable, ["user"] = userTable };

            // Make folder object
            var metaFolder = new JObject { ["type"] = "no.sintef.garmin" };
            var userFolder = new JObject { ["full_table"] = rootTable };
            userFolder["filename"] = _fileName;

            // Place objects into root
            root["meta"] = metaFolder;
            root["user"] = userFolder;

            bool result = await WriteTable(fileId, writer);
            return result;

        }
    }

    public class GarminParser
    {
        IReadSeekStreamFactory _readerFactory;

        internal GarminParser(IReadSeekStreamFactory readerFactory)
        {
            _readerFactory = readerFactory;

        }

        internal Dictionary<string, Array> ReadData()
        {
            Dictionary<string, Array> dataList = null;

            using (var s = Task.Run(() => _readerFactory.GetReadStream()).GetAwaiter().GetResult())
            {
                var tpList = ParseFile(s);
                dataList = ConvertTrackpoints(tpList);
            }
            return dataList;

        }


        private List<TrackPoint> ParseFile(Stream s)
        {
            List<TrackPoint> parseList = null;

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
                                                        LatitudeDegrees = Convert.ToDouble(elp.Element(ns1 + "LatitudeDegrees")?.Value),
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

                parseList = trackpointList;
            }
            return parseList;
        }
        private Dictionary<string, Array> ConvertTrackpoints(List<TrackPoint> tpList)
        {
            var faultyEntries = new List<TrackPoint>();

            // Convert timeString to Epoch
            CultureInfo enUS = new CultureInfo("en-US");
            string[] dateFormatArr = { "yyyy-MM-ddTHH:mm:ss.fffK", "yyyy-MM-ddTHH:mm:ssK" };
            foreach (TrackPoint entry in tpList)
            {
                var epochZero = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                if (DateTime.TryParseExact(entry.TimeString, dateFormatArr, enUS, DateTimeStyles.AssumeLocal, out var dt))
                {
                    var dtUtc = TimeZoneInfo.ConvertTimeToUtc(dt);
                    var epdt = dtUtc.Ticks - epochZero.Ticks;
                    entry.TimeWorldClock = epdt / 10;
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


            Dictionary<string, Array> locData = new Dictionary<string, Array>();

            // Wrap up and store result
            locData.Add("time", GenerateColumnArray(tpList, entry => entry.TimeWorldClock));
            locData.Add("altitude", GenerateColumnArray(tpList, entry => entry.AltitudeMeters));
            locData.Add("dist", GenerateColumnArray(tpList, entry => entry.DistanceMeters));
            locData.Add("speed", GenerateColumnArray(tpList, entry => entry.SpeedMS));
            locData.Add("HR", GenerateColumnArray(tpList, entry => entry.HeartRateBpm));
            locData.Add("latitude", GenerateColumnArray(tpList, entry => entry.LatitudeDegrees));
            locData.Add("longitude", GenerateColumnArray(tpList, entry => entry.LongitudeDegrees));

            return locData;
        }

        private T[] GenerateColumnArray<T>(List<TrackPoint> trackPoints, Func<TrackPoint, T> fetchValue)
        {
            T[] data = new T[trackPoints.Count];
            var i = 0;
            foreach (var trackPoint in trackPoints)
            {
                data[i++] = fetchValue(trackPoint);
            }
            return data;
        }

    }

    public class TrackPoint
    {
        public string TimeString { set; get; }
        public long TimeWorldClock { set; get; }
        public double AltitudeMeters { get; set; }
        public double DistanceMeters { get; set; }
        public double SpeedMS { get; set; }
        public double HeartRateBpm { get; set; }
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

}