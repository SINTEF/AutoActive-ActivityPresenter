using SINTEF.AutoActive.Archive;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using MimeMapping;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.FileSystem;
#if !DEBUG
using Xamarin.Forms; // Only used by DependencyService
#endif

namespace SINTEF.AutoActive.Plugins.ArchivePlugins.Video
{
    public class ArchiveVideo : ArchiveStructure, ISaveable
    {
        private readonly ZipEntry _zipEntry;
        private readonly Archive.Archive _archive;
        public override string Type => "no.sintef.video";
        private readonly ArchiveVideoVideo _video;
        private readonly IReadSeekStreamFactory _readerFactory = null;

        public ArchiveVideo(JObject json, Archive.Archive archive, Guid sessionId) : base(json)
        {
            var pathArr = Meta["attachments"].ToObject<string[]>() ?? throw new ArgumentException("Video is missing 'attachments'");
            var path = "" + sessionId + pathArr[0];

            _archive = archive;

            var offset = Meta["start_time"].Value<long>();

            // Find the file in the archive
            _zipEntry = _archive.FindFile(path) ?? throw new ZipException($"Video file '{path}' not found in archive");

            var estimatedVideoLength = 0L;
            if (Meta.TryGetValue("video_length", out var value))
            {
                estimatedVideoLength = value.Value<long>();
            }

            // Create the video datapoint
            _video = new ArchiveVideoVideo(_zipEntry, _archive, path, offset, estimatedVideoLength) {Name = "Video"};

            AddDataPoint(_video);
            IsSaved = true;
        }

        public ArchiveVideo(JObject json, IReadSeekStreamFactory readerFactory) : base(json)
        {
            Meta["type"] = Type;

            _readerFactory = readerFactory;
            var estimatedVideoLength = 0L;
            if (Meta.TryGetValue("video_length", out var value))
            {
                estimatedVideoLength = value.Value<long>();
            }

            _video = new ArchiveVideoVideo(readerFactory, readerFactory.Name, Meta["start_time"].Value<long>(), estimatedVideoLength) {Name = "Video"};
            Name = readerFactory.Name;

            AddDataPoint(_video);
            IsSaved = false;
        }

        public bool IsSaved { get; }

        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {

            Stream stream;
            string fileId;
            if (_readerFactory == null)
            {
                var pathArr = Meta["attachments"].ToObject<string[]>() ??
                              throw new ArgumentException("Video is missing 'attachments'");
                stream = await _archive.OpenFile(_zipEntry);
                fileId = pathArr[0];
            }
            else
            {
                stream = await _readerFactory.GetReadStream();
                // TODO: give a better name
                fileId = "video";
                Meta["attachments"] = new JArray(new object[] {fileId});
            }

            writer.StoreFileId(stream, fileId);

            var offset = _video.VideoTime.Offset;

            // TODO AUTOACTIVE-58 - Generalize copy of previous metadata for save

            // Copy previous
            root["meta"] = Meta;
            root["user"] = User;

            // Overwrite potentially changed
            root["meta"]["start_time"] = offset;
            root["meta"]["video_length"] = _video.VideoTime.VideoLength;
            // TODO root["meta"]["is_world_clock"] =  ;
            // TODO root["meta"]["synced_to"] =  ;

            return true;
        }
    }

    public class ArchiveVideoVideo : IDataPoint
    {
        private readonly ZipEntry _zipEntry;
        private readonly Archive.Archive _archive;

        public string URI { get; }
        public Type DataType => throw new NotImplementedException();
        public string Name { get; set; }
        public ArchiveVideoTime VideoTime { get; }
        public ITimePoint Time => VideoTime;
        
        private readonly IReadSeekStreamFactory _streamFactory;

        public ArchiveVideoVideo(ZipEntry zipEntry, Archive.Archive archive, string path, long startTime, long videoLength)
        {
            _zipEntry = zipEntry;
            _archive = archive;
            URI = path;
            _streamFactory = _archive.OpenFileFactory(_zipEntry);
            VideoTime = new ArchiveVideoTime(_streamFactory, path, startTime, videoLength);
        }

        public ArchiveVideoVideo(IReadSeekStreamFactory streamFactory, string path, long startTime, long videoLength)
        {
            _streamFactory = streamFactory;
            URI = path;
            Name = _streamFactory.Name;
            VideoTime = new ArchiveVideoTime(streamFactory, path, startTime, videoLength);
        }

        public Task<IDataViewer> CreateViewer()
        {
            return Task.FromResult((IDataViewer)new ArchiveVideoVideoViewer(this));
        }

        public (IReadSeekStreamFactory, string mime) GetStreamFactory()
        {
            return (_streamFactory, MimeUtility.GetMimeMapping(URI));
        }
    }

    public class ArchiveVideoTime : ITimePoint
    {
        private readonly IReadSeekStreamFactory _streamFactory;
        private readonly string _path;

        public event EventHandler<long> OffsetChanged;
        private long _offset;
        public long Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                OffsetChanged?.Invoke(this, _offset);
            }
        }

        public long VideoLength { get; }

        public double Scale;
        private IVideoLengthExtractor _videoLengthExtractor;
        private async Task<IVideoLengthExtractor> GetVideoLengthExtractor()
        {
            if (_videoLengthExtractor != null) return _videoLengthExtractor;
#if DEBUG
            var factory = DependencyHandler.GetInstance<IVideoLengthExtractorFactory>();
#else
            var factory = DependencyService.Get<IVideoLengthExtractorFactory>();
#endif
            if (factory == null) throw new NotImplementedException();

            var mime = MimeUtility.GetMimeMapping(_path);
            _videoLengthExtractor = await factory.CreateVideoDecoder(_streamFactory, mime, VideoLength);
            return _videoLengthExtractor;
        }

        internal ArchiveVideoTime(IReadSeekStreamFactory streamFactory, string path, long startTime, long videoLength)
        {
            _streamFactory = streamFactory;
            _path = path;
            Offset = startTime;
            VideoLength = videoLength;
        }

        public bool IsSynchronizedToWorldClock => false; // FIXME: How do we store the sync?

        private readonly List<ArchiveVideoTimeViewer> _viewers = new List<ArchiveVideoTimeViewer>();

        public async Task<ITimeViewer> CreateViewer()
        {
            var viewer = new ArchiveVideoTimeViewer(this, await GetVideoLengthExtractor());
            
            _viewers.Add(viewer);
            return viewer;
        }

        public void TransformTime(long offset, double scale)
        {
            // TODO: Trigger TimeViewers' TimeChanged
            Offset += offset;
            Scale = scale;
            foreach (var viewer in _viewers)
            {
                viewer.Start = Offset;
            }
        }
    }

    public class ArchiveVideoTimeViewer : ITimeViewer
    {
        private readonly ArchiveVideoTime _time;
        private readonly IVideoLengthExtractor _videoLengthExtractor;

        internal ArchiveVideoTimeViewer(ArchiveVideoTime time, IVideoLengthExtractor videoLengthExtractor)
        {
            _time = time;
            _videoLengthExtractor = videoLengthExtractor;
            _videoLength = _videoLengthExtractor.ReportedLength;
            LoadVideoLength();
        }

        private long _videoLength;

        private async void LoadVideoLength()
        {
            _videoLength = await _videoLengthExtractor.GetLengthAsync();
            if (_videoLength == 0L)
            {
                Debug.WriteLine("Could not get video length");
            }
            TimeChanged?.Invoke(this, Start, End);
            Debug.WriteLine($"Change invoked {Start}->{End}");
        }

        public void UpdatedTimeIndex() { }

        public ITimePoint TimePoint => _time;

        public long Start
        {
            get => _time.Offset;
            set
            {
                _time.Offset = value;
                TimeChanged?.Invoke(this, Start, End);
            }
        }

        public long End => Start + _videoLength;

        public event TimeViewerWasChangedHandler TimeChanged;
    }

    public class ArchiveVideoVideoViewer : IDataViewer
    {
        public ArchiveVideoVideo Video;
        public IDataPoint DataPoint => Video;
        public long CurrentTimeRangeFrom { get; }
        public long CurrentTimeRangeTo { get; }
        public event DataViewerWasChangedHandler Changed;

        public long PreviewPercentage { get; set; }
        public void SetTimeRange(long from, long to)
        {
            // This event is handled directly by the video handler
        }

        internal ArchiveVideoVideoViewer(ArchiveVideoVideo video)
        {
            Video = video;
        }
    }

    [ArchivePlugin("no.sintef.video")]
    public class ArchiveVideoPlugin : IArchivePlugin
    {
        public Task<ArchiveStructure> CreateFromJSON(JObject json, Archive.Archive archive, Guid sessionId)
        {
            return Task.FromResult<ArchiveStructure>(new ArchiveVideo(json, archive, sessionId));
        }
    }
}
