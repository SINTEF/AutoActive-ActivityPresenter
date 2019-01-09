using SINTEF.AutoActive.Archive;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus;
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
    public class ArchiveVideo : ArchiveStructure
    {
        public override string Type => "no.sintef.video";

        internal ArchiveVideo(JObject json, Archive.Archive archive) : base(json)
        {
            var path = Meta["path"].ToObject<string>() ?? throw new ArgumentException("Video is missing 'path'");

            // Find the file in the archive
            var zipEntry = archive.FindFile(path) ?? throw new ZipException($"Video file '{path}' not found in archive");

            // Create the video datapoint
            var video = new ArchiveVideoVideo(zipEntry, archive, path);
            video.Name = "Video";
            AddDataPoint(video);
        }

        /*
        protected override void ToArchiveJSON(JObject meta, JObject user)
        {
            throw new NotImplementedException();
        }
        */
    }

    public class ArchiveVideoVideo : IDataPoint
    {
        ZipEntry zipEntry;
        Archive.Archive archive;
        string path;
        ArchiveVideoTime time;


        internal ArchiveVideoVideo(ZipEntry zipEntry, Archive.Archive archive, string path)
        {
            this.zipEntry = zipEntry;
            this.archive = archive;
            this.path = path;
            time = new ArchiveVideoTime(zipEntry, archive, path);
        }

        public Type DataType => throw new NotImplementedException();

        public string Name { get; set; }

        public ITimePoint Time => time;

        public async Task<IDataViewer> CreateViewer()
        {
            var factory = DependencyHandler.GetInstance<IVideoDecoderFactory>();
            if (factory != null)
            {
                var mime = MimeUtility.GetMimeMapping(path);
                var decoder = await factory.CreateVideoDecoder(archive.OpenFileFactory(zipEntry), mime);
                Debug.WriteLine("Decoder created!");
                return new ArchiveVideoVideoViewer(this, decoder);
            }
            throw new NotImplementedException();
        }
    }

    public class ArchiveVideoTime : ITimePoint
    {
        ZipEntry zipEntry;
        Archive.Archive archive;
        string path;

        internal ArchiveVideoTime(ZipEntry zipEntry, Archive.Archive archive, string path)
        {
            this.zipEntry = zipEntry;
            this.archive = archive;
            this.path = path;
        }

        public bool IsSynchronizedToWorldClock => false; // FIXME: How do we store the sync?

        public async Task<ITimeViewer> CreateViewer()
        {
            var factory = DependencyHandler.GetInstance<IVideoDecoderFactory>();
            if (factory != null)
            {
                var mime = MimeUtility.GetMimeMapping(path);
                var decoder = await factory.CreateVideoDecoder(archive.OpenFileFactory(zipEntry), mime);
                return new ArchiveVideoTimeViewer(this, decoder);
            }
            throw new NotImplementedException();
        }
    }

    public class ArchiveVideoTimeViewer : ITimeViewer
    {
        ArchiveVideoTime time;
        IVideoDecoder decoder;

        internal ArchiveVideoTimeViewer(ArchiveVideoTime time, IVideoDecoder decoder)
        {
            this.time = time;
            this.decoder = decoder;
            LoadTime();
        }

        private async void LoadTime()
        {
            End = await decoder.GetLengthAsync();
            TimeChanged?.Invoke(this, Start, End);
            Debug.WriteLine($"Change invoked {Start}->{End}");
        }

        public void UpdatedTimeIndex() { }

        public ITimePoint TimePoint => time;

        public long Start { get; private set; } = 0;
        public long End { get; private set; } = 0;

        public event TimeViewerWasChangedHandler TimeChanged;
    }

    public class ArchiveVideoVideoViewer : IImageViewer
    {
        ArchiveVideoVideo video;
        BufferedVideoDecoder decoder;

        VideoDecoderFrame currentFrame;

        readonly object locker = new object();
        CancellationTokenSource cancellation;

        public event DataViewerWasChangedHandler Changed;

        public IDataPoint DataPoint => video;

        public long CurrentTimeRangeFrom { get; private set; }
        public long CurrentTimeRangeTo { get; private set; }

        internal ArchiveVideoVideoViewer(ArchiveVideoVideo video, IVideoDecoder decoder)
        {
            this.video = video;
            this.decoder = new BufferedVideoDecoder(decoder);
        }

        public Task SetSize(uint width, uint height)
        {
            return decoder.SetSizeAsync(width, height);
        }

        public ImageFrame GetCurrentImage()
        {
            return new ImageFrame(currentFrame.Width, currentFrame.Height, currentFrame.Frame);
        }

        public void SetTimeRange(long from, long to)
        {
            UpdateCurrentFrame(from);
        }

        async void UpdateCurrentFrame(long time)
        {
            CancellationToken token;
            lock (locker)
            {
                // Stop any previous frame grabbing
                if (cancellation != null)
                {
                    cancellation.Cancel();
                }
                cancellation = new CancellationTokenSource();
                token = cancellation.Token;
            }
            try
            {
                var frame = await decoder.GetFrameAtAsync(time, cancellation.Token);
                currentFrame = frame;
                Changed?.Invoke(this);
            }
            catch (OperationCanceledException) { }
        }
    }

    [ArchivePlugin("no.sintef.video")]
    public class ArchiveVideoPlugin : IArchivePlugin
    {
        public Task<ArchiveStructure> CreateFromJSON(JObject json, Archive.Archive archive)
        {
            return Task.FromResult<ArchiveStructure>(new ArchiveVideo(json, archive));
        }
    }
}
