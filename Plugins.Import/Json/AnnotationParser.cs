using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Implementations;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using Newtonsoft.Json;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Archive;
using ICSharpCode.SharpZipLib.Zip;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.Databus;

namespace SINTEF.AutoActive.Plugins.Import.Json
{
    [ImportPlugin(".json")]
    public class ImportAnnotationPlugin : IImportPlugin
    {
        public async Task<bool> CanParse(IReadSeekStreamFactory readerFactory)
        {
            var stream = await readerFactory.GetReadStream();
            using (var streamReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var serializer = new JsonSerializer();
                var json = (JToken)serializer.Deserialize(jsonReader);
                if (json["AutoActiveType"].ToObject<string>() != "Annotation")
                {
                    return false;
                }
            }
            return true;
        }

        public void GetExtraConfigurationParameters(Dictionary<string, (object, string)> parameters)
        { }

        public async Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory,
            Dictionary<string, object> parameters)
        {
            var importer = new AnnotationProvider(parameters, readerFactory.Name);
            importer.ParseFile(await readerFactory.GetReadStream());
            return importer;
        }
    }

    [ArchivePlugin("no.sintef.annotation")]
    public class AnnotationProvider : BaseDataProvider, IArchivePlugin, ISaveable
    {
        public AnnotationSet AnnotationSet
        {
            get => _annotationsBacking;
            set
            {
                if(_annotationsBacking != null)
                {
                    RemoveDataPoint(DataPoint);
                }
                var annotationSet = value;
                var annotations = annotationSet.Annotations;

                _timeData = annotations.Select(annotation => annotation.Timestamp).ToList();
                _annotationData = annotations.Select(annotation => annotation.Type).ToList();

                IsSaved = false;

                var isWorldSynchronized = annotationSet.IsWorldSynchronized;

                _timePoint = new BaseTimePoint(_timeData, isWorldSynchronized);

                var uri = "Annotations";
                _dataPoint = new AnnotationDataPoint("Annotations", _annotationData, annotationSet, _timePoint, uri, null);

                _annotationsBacking = annotationSet;
                AddDataPoint(_dataPoint);
            }
        }
        private AnnotationSet _annotationsBacking;

        private List<long> _timeData;
        private List<int> _annotationData;
        private BaseTimePoint _timePoint;
        private AnnotationDataPoint _dataPoint;

        public AnnotationDataPoint DataPoint => _dataPoint;

        public AnnotationProvider(Dictionary<string, object> parameters, string filename)
        {
            Name = parameters["Name"] as string;
        }

        public AnnotationProvider()
        {
            Name = "AnnotationProvider";
        }

        public static AnnotationProvider CreateNew()
        {
            var provider = new AnnotationProvider
            {
                AnnotationSet = new AnnotationSet()
            };
            return provider;
        }

        protected virtual void PostProcessData(List<string> names, List<Type> types, List<Array> data)
        {
        }

        protected override void DoParseFile(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                AnnotationSet = JsonConvert.DeserializeObject<AnnotationSet>(streamReader.ReadToEnd());
            }
        }

        public void AddAnnotation(long timestamp, int annotationId)
        {
            _timeData.Add(timestamp);
            _annotationData.Add(annotationId);
            _timePoint.TriggerChanged();
            _dataPoint.TriggerDataChanged();
            AnnotationSet.Annotations.Add(new AnnotationPoint(timestamp, annotationId));
            IsSaved = false;
        }

        public bool IsSynchronizedToWorldClock
        {
            get; set;
        }

        public bool IsSaved { get; private set; }

        public async Task<IDataStructure> CreateFromJSON(JObject json, Archive.Archive archive, Guid sessionId)
        {
            // Find the properties in the JSON
            ArchiveStructure.GetUserMeta(json, out var meta, out var user);
            var pathArr = meta["attachments"].ToObject<string[]>() ?? throw new ArgumentException("Table is missing 'attachments'");
            var path = "" + sessionId + pathArr[0];

            var zipEntry = archive.FindFile(path) ?? throw new ZipException($"Table file '{path}' not found in archive");

            using (var stream = await archive.OpenFile(zipEntry))
            {
                ParseFile(stream);
            }
            return this;
        }

        public Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            // TODO: merge annotation files? Would then need to re-number all annotations
            var fileName = "/Annotations/annotations.json";


            var data = JsonConvert.SerializeObject(AnnotationSet);
            using (var ms = new MemoryStream())
            using (var sw = new StreamWriter(ms))
            {
                sw.Write(data);
                sw.Flush();
                ms.Position = 0;
                writer.StoreFileId(ms, fileName);
            }

            var metaTable = new JObject { ["type"] = "no.sintef.annotation" };
            metaTable["attachments"] = new JArray(new object[] { fileName });
            root["meta"] = metaTable;
            root["user"] = new JObject();
            IsSaved = true;

            return Task.FromResult(true);
        }

        public List<AnnotationPoint> FindClosestAnnotation(long timestamp, long window = 0)
        {
            var closest = new List<AnnotationPoint>();

            var closestTime = long.MaxValue;
            foreach (var annotation in AnnotationSet.Annotations)
            {
                var diffTime = Math.Abs(annotation.Timestamp - timestamp);
                if (diffTime < closestTime)
                {
                    closestTime = diffTime;
                    closest.RemoveAll(ann => Math.Abs(ann.Timestamp - timestamp) > window);
                    closest.Add(annotation);
                    continue;
                }

                if (Math.Abs(diffTime - closestTime) < window)
                {
                    closest.Add(annotation);
                }
            }

            closest.Sort((a, b) => Math.Abs(a.Timestamp - timestamp).CompareTo(Math.Abs(b.Timestamp - timestamp)));

            return closest;
        }

        public void RemoveAnnotation(AnnotationPoint annotationPoint)
        {
            AnnotationSet.Annotations.Remove(annotationPoint);


            for (var i = 0; i < _timeData.Count; i++)
            {
                if (_timeData[i] == annotationPoint.Timestamp && _annotationData[i] == annotationPoint.Type)
                {
                    _timeData.RemoveAt(i);
                    _annotationData.RemoveAt(i);
                    break;
                }
            }

            _timePoint.TriggerChanged();
            _dataPoint.TriggerDataChanged();
            IsSaved = false;
        }

        public static AnnotationProvider GetAnnotationProvider(bool isSynchronizedToWolrdClock)
        {
            var annotationProvider = DataRegistry.FindFirstDataStructure<AnnotationProvider>(DataRegistry.Providers);

            // If no annotations are found, make a new
            if (annotationProvider == null)
            {
                annotationProvider = CreateNew();
                annotationProvider.IsSynchronizedToWorldClock = isSynchronizedToWolrdClock;
                DataRegistry.Register(annotationProvider);
            }

            return annotationProvider;
        }
    }

    public class AnnotationDataPoint : BaseDataPoint<int>
    {
        private readonly Task<AnnotationSet> _annotationSetLoader;

        private AnnotationSet _annotationSet;

        public AnnotationSet AnnotationSet => _annotationSet;

        public AnnotationDataPoint(string name, Task<List<int>> loader, Task<AnnotationSet> annotationSetLoader, BaseTimePoint time, string uri, string unit) : base(name, loader, time, uri, unit)
        {
            _annotationSetLoader = annotationSetLoader;
        }

        public AnnotationDataPoint(string name, List<int> data, AnnotationSet annotationSet, BaseTimePoint time, string uri, string unit) : base(name, data, time, uri, unit)
        {
            _annotationSet = annotationSet;
        }

        protected override BaseDataViewer CreateDataViewer()
        {
            if (_annotationSet == null && _annotationSetLoader != null)
            {
                _annotationSetLoader.Wait();
                _annotationSet = _annotationSetLoader.Result;
            }
            return new AnnotationDataViewer(new BaseTimeViewer(Time), this);
        }
    }

    public class AnnotationDataViewer : BaseTimeSeriesViewer<int>
    {
        private AnnotationDataPoint _annotationDataPoint;
        public AnnotationSet AnnotationSet => _annotationDataPoint.AnnotationSet;

        public AnnotationDataViewer(BaseTimeViewer timeViewer, AnnotationDataPoint dataPoint) : base(timeViewer, dataPoint)
        {
            _annotationDataPoint = dataPoint;
        }
    }

    public class AnnotationPoint
    {
        public AnnotationPoint(long timestamp, int type)
        {
            Timestamp = timestamp;
            Type = type;
        }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        public override string ToString()
        {
            return $"{Type} @ {TimeFormatter.FormatTime(Timestamp)}";
        }
    }

    public class AnnotationSet
    {
        public AnnotationSet()
        {
            AutoActiveType = "Annotation";
            Version = "1.0.0";

            AnnotationTypeComments = new Dictionary<int, string>();
            AnnotationNames = new Dictionary<int, string>();
            AnnotationTags = new Dictionary<int, string>();
            Annotations = new List<AnnotationPoint>();
        }
        public string AutoActiveType { get; set; }

        [JsonProperty("is_world_synchronized")]
        public bool IsWorldSynchronized { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("annotation_type_comments")]
        public Dictionary<int, string> AnnotationTypeComments { get; set; }
        [JsonProperty("annotation_names")]
        public Dictionary<int, string> AnnotationNames { get; set; }
        [JsonProperty("annotation_tags")]
        public Dictionary<int, string> AnnotationTags { get; set; }
        [JsonProperty("annotations")]
        public List<AnnotationPoint> Annotations { get; set; }
    }
}
