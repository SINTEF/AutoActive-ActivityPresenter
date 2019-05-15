using SINTEF.AutoActive.Archive;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using MimeMapping;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.ArchivePlugins.Video
{
    public class ArchiveVideo : ArchiveStructure, ISaveable
    {
        private readonly ZipEntry _zipEntry;
        private readonly Archive.Archive _archive;
        public override string Type => "no.sintef.video";

        internal ArchiveVideo(JObject json, Archive.Archive archive, Guid sessionId) : base(json)
        {
            var pathArr = Meta["attachments"].ToObject<string[]>() ?? throw new ArgumentException("Video is missing 'attachments'");
            var path = "" + sessionId + pathArr[0];

            _archive = archive;

            // Find the file in the archive
            _zipEntry = _archive.FindFile(path) ?? throw new ZipException($"Video file '{path}' not found in archive");

            // Create the video datapoint
            var video = new ArchiveVideoVideo(_zipEntry, _archive, path) {Name = "Video"};
            AddDataPoint(video);
            IsSaved = true;
        }

        public bool IsSaved { get; }
        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            var pathArr = Meta["attachments"].ToObject<string[]>() ?? throw new ArgumentException("Video is missing 'attachments'");

            var stream = await _archive.OpenFile(_zipEntry);

            writer.StoreFileId(stream, pathArr[0]);

            // TODO AUTOACTIVE-58 - Generalize copy of previous metadata for save

            // Copy previous
            root["meta"] = Meta;
            root["user"] = User;

            // Overwrite potentially changed
            // TODO root["meta"]["start_time"] =  ;
            // TODO root["meta"]["is_world_clock"] =  ;
            // TODO root["meta"]["synced_to"] =  ;

            return true;
        }
    }

    public class ArchiveVideoVideo : IDataPoint
    {
        private readonly ZipEntry _zipEntry;
        private readonly Archive.Archive _archive;
        private readonly string _path;
        private readonly ArchiveVideoTime _time;

        internal ArchiveVideoVideo(ZipEntry zipEntry, Archive.Archive archive, string path)
        {
            _zipEntry = zipEntry;
            _archive = archive;
            _path = path;
            _time = new ArchiveVideoTime(zipEntry, archive, path);
        }

        public Type DataType => throw new NotImplementedException();

        public string Name { get; set; }

        public ITimePoint Time => _time;

        public Task<IDataViewer> CreateViewer()
        {
            return Task.FromResult((IDataViewer)new ArchiveVideoVideoViewer(this));
        }

        public (IReadSeekStreamFactory streamFactory, string mime) GetStreamFactory()
        {
            return (_archive.OpenFileFactory(_zipEntry), MimeUtility.GetMimeMapping(_path));
        }
    }

    public class ArchiveVideoTime : ITimePoint
    {
        private readonly ZipEntry _zipEntry;
        private readonly Archive.Archive _archive;
        private readonly string _path;
        public long Offset;
        public double Scale;
        private IVideoLengthExtractor _videoLengthExtractor;
        private async Task<IVideoLengthExtractor> GetDecoder()
        {
            if (_videoLengthExtractor != null) return _videoLengthExtractor;
            var factory = DependencyHandler.GetInstance<IVideoLengthExtractorFactory>();
            if (factory == null) throw new NotImplementedException();

            var mime = MimeUtility.GetMimeMapping(_path);
            _videoLengthExtractor = await factory.CreateVideoDecoder(_archive.OpenFileFactory(_zipEntry), mime);
            return _videoLengthExtractor;
        }

        internal ArchiveVideoTime(ZipEntry zipEntry, Archive.Archive archive, string path)
        {
            _zipEntry = zipEntry;
            _archive = archive;
            _path = path;
        }

        public bool IsSynchronizedToWorldClock => false; // FIXME: How do we store the sync?

        public async Task<ITimeViewer> CreateViewer()
        {
            return new ArchiveVideoTimeViewer(this, await GetDecoder());
        }

        public void TransformTime(long offset, double scale)
        {
            // TODO: Trigger TimeViewers' TimeChanged
            Offset = offset;
            Scale = scale;
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
            LoadTime();
        }

        private async void LoadTime()
        {
            End = await _videoLengthExtractor.GetLengthAsync();
            TimeChanged?.Invoke(this, Start, End);
            Debug.WriteLine($"Change invoked {Start}->{End}");
        }

        public void UpdatedTimeIndex() { }

        public ITimePoint TimePoint => _time;

        public long Start => _time.Offset;
        public long End { get; private set; } = 0;

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
