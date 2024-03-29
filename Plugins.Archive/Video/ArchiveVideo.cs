﻿using SINTEF.AutoActive.Archive;
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

                // TODO: give a better name?
                fileId = "/videos" + "/" + Name + "." + Guid.NewGuid();
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

        public Archive.Archive Archive { get => _archive; }

        public string URI { get; }
        public Type DataType => typeof(ArchiveVideoVideo);
        public string Name { get; set; }
        public ArchiveVideoTime VideoTime { get; }
        public ITimePoint Time => VideoTime;
        public string Unit { get; set; }
        private readonly IReadSeekStreamFactory _streamFactory;

        public event EventHandler DataChanged;

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
            if (_videoLengthExtractor != null)
            {
                var task = _videoLengthExtractor.GetLengthAsync();
                if (task.IsCompleted && task.Result != 0)
                {
                    return _videoLengthExtractor;
                }

                _videoLengthExtractor.Restart();
                return _videoLengthExtractor;
            }
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
        public long VideoPlaybackOffset { get; set; }

        private readonly List<ArchiveVideoTimeViewer> _viewers = new List<ArchiveVideoTimeViewer>();

        public async Task<ITimeViewer> CreateViewer()
        {
            await LoadVideoLength();

            var viewer = new ArchiveVideoTimeViewer(this);
            _viewers.Add(viewer);
            return viewer;
        }

        private async Task LoadVideoLength()
        {
            _videoLength = await (await GetVideoLengthExtractor()).GetLengthAsync();
            if (_videoLength == 0L)
            {
                Debug.WriteLine("Could not get video length");
            }
            foreach (var viewer in _viewers)
            {
                viewer.TriggerTimeChanged(Start,End);
            }
            Debug.WriteLine($"Change invoked {Start}->{End}");
        }

        public void TransformTime(long offset, double scale)
        {
            Offset += offset;
#if VIDEO_TIME_COMPENSATION
            //TODO: Check sign of videoplayblackoffset
            Offset += VideoPlaybackOffset;
#endif
            Scale = scale;

            foreach (var viewer in _viewers)
            {
                viewer.UpdatedTimeIndex();
            }
        }

        private long _videoLength;

        public long Start
        {
            get => Offset;
        }

        public long End => Start + _videoLength;
    }

    public class ArchiveVideoTimeViewer : ITimeViewer
    {
        private readonly ArchiveVideoTime _time;

        internal ArchiveVideoTimeViewer(ArchiveVideoTime time)
        {
            _time = time;
        }

        public void UpdatedTimeIndex()
        {
            TimeChanged?.Invoke(this, Start, End);
        }

        internal void TriggerTimeChanged(long start, long end)
        {
            TimeChanged?.Invoke(this, Start, End);
        }

        public long Start => _time.Start;
        public long End => _time.End;

        public ITimePoint TimePoint => _time;

        public event TimeViewerWasChangedHandler TimeChanged;
    }

    public class ArchiveVideoVideoViewer : IDataViewer
    {
        public ArchiveVideoVideo Video;

        public IDataPoint DataPoint => Video;
        public long CurrentTimeRangeFrom { get; }
        public long CurrentTimeRangeTo { get; }

        public long PreviewPercentage { get; set; }
        public void SetTimeRange(long from, long to)
        {
            // This event is handled directly by the video handler
        }

        public event EventHandler Changed;

        internal ArchiveVideoVideoViewer(ArchiveVideoVideo video)
        {
            Video = video;
        }
    }

    [ArchivePlugin("no.sintef.video")]
    public class ArchiveVideoPlugin : IArchivePlugin
    {
        public Task<IDataStructure> CreateFromJSON(JObject json, Archive.Archive archive, Guid sessionId)
        {
            return Task.FromResult<IDataStructure>(new ArchiveVideo(json, archive, sessionId));
        }
    }
}
