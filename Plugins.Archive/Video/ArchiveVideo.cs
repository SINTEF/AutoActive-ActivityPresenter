using SINTEF.AutoActive.Archive;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using MimeMapping;
using Newtonsoft.Json.Linq;

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
            var video = new ArchiveVideoVideo(_zipEntry, _archive, path);
            video.Name = "Video";
            AddDataPoint(video);
            IsSaved = true;
        }

        public bool IsSaved { get; }
        public async Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            var pathArr = Meta["attachments"].ToObject<string[]>() ?? throw new ArgumentException("Video is missing 'attachments'");

            var stream = await _archive.OpenFile(_zipEntry);

            writer.StoreFileId(stream, pathArr[0]);

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

        public async Task<IDataViewer> CreateViewer()
        {
            var factory = DependencyHandler.GetInstance<IVideoDecoderFactory>();
            if (factory == null) throw new NotImplementedException();

            var mime = MimeUtility.GetMimeMapping(_path);
            var decoder = await factory.CreateVideoDecoder(_archive.OpenFileFactory(_zipEntry), mime);
            Debug.WriteLine("Decoder created!");
            return new ArchiveVideoVideoViewer(this, decoder);
        }
    }

    public class ArchiveVideoTime : ITimePoint
    {
        private readonly ZipEntry _zipEntry;
        private readonly Archive.Archive _archive;
        private readonly string _path;
        public long Offset;
        public double Scale;

        internal ArchiveVideoTime(ZipEntry zipEntry, Archive.Archive archive, string path)
        {
            _zipEntry = zipEntry;
            _archive = archive;
            _path = path;
        }

        public bool IsSynchronizedToWorldClock => false; // FIXME: How do we store the sync?

        public async Task<ITimeViewer> CreateViewer()
        {
            var factory = DependencyHandler.GetInstance<IVideoDecoderFactory>();

            if (factory == null) throw new NotImplementedException();

            var mime = MimeUtility.GetMimeMapping(_path);
            var decoder = await factory.CreateVideoDecoder(_archive.OpenFileFactory(_zipEntry), mime);
            return new ArchiveVideoTimeViewer(this, decoder);
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
        private readonly IVideoDecoder _decoder;

        internal ArchiveVideoTimeViewer(ArchiveVideoTime time, IVideoDecoder decoder)
        {
            _time = time;
            _decoder = decoder;
            LoadTime();
        }

        private async void LoadTime()
        {
            End = await _decoder.GetLengthAsync();
            TimeChanged?.Invoke(this, Start, End);
            Debug.WriteLine($"Change invoked {Start}->{End}");
        }

        public void UpdatedTimeIndex() { }

        public ITimePoint TimePoint => _time;

        public long Start { get; private set; } = 0;
        public long End { get; private set; } = 0;

        public event TimeViewerWasChangedHandler TimeChanged;
    }

    public class ArchiveVideoVideoViewer : IImageViewer
    {
        private readonly ArchiveVideoVideo _video;
        private readonly BufferedVideoDecoder _decoder;

        private VideoDecoderFrame _currentFrame;

        private readonly object _locker = new object();
        private CancellationTokenSource _cancellation;

        public event DataViewerWasChangedHandler Changed;

        public IDataPoint DataPoint => _video;

        public long CurrentTimeRangeFrom { get; private set; }
        public long CurrentTimeRangeTo { get; private set; }
        public long PreviewPercentage { get; set; }

        internal ArchiveVideoVideoViewer(ArchiveVideoVideo video, IVideoDecoder decoder)
        {
            _video = video;
            _decoder = new BufferedVideoDecoder(decoder);
        }

        public Task SetSize(uint width, uint height)
        {
            return _decoder.SetSizeAsync(width, height);
        }

        public ImageFrame GetCurrentImage()
        {
            return new ImageFrame(_currentFrame.Width, _currentFrame.Height, _currentFrame.Frame);
        }

        public void SetTimeRange(long from, long to)
        {
            UpdateCurrentFrame(from);
        }

        private async void UpdateCurrentFrame(long time)
        {
            lock (_locker)
            {
                // Stop any previous frame grabbing
                _cancellation?.Cancel();
                _cancellation = new CancellationTokenSource();
            }
            try
            {
                var frame = await _decoder.GetFrameAtAsync(time, _cancellation.Token);
                _currentFrame = frame;
                Changed?.Invoke(this);
            }
            catch (OperationCanceledException) { }
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
