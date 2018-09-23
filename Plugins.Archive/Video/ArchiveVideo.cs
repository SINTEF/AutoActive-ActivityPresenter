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
using Xamarin.Forms;

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
        protected override void RegisterContents(DataStructureAddedToHandler dataStructureAdded, DataPointAddedToHandler dataPointAdded)
        {
            dataPointAdded?.Invoke(video, this);
        }

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

        internal ArchiveVideoVideo(ZipEntry zipEntry, Archive.Archive archive, string path)
        {
            this.zipEntry = zipEntry;
            this.archive = archive;
            this.path = path;
        }

        public Type DataType => throw new NotImplementedException();

        public string Name { get; set; }

        public async Task<IDataViewer> CreateViewerIn(DataViewerContext context)
        {
            var factory = DependencyService.Get<IVideoDecoderFactory>();
            if (factory != null)
            {
                var mime = MimeUtility.GetMimeMapping(path);
                var decoder = await factory.CreateVideoDecoder(archive.OpenFileFactory(zipEntry), mime);
                Debug.WriteLine("Decoder created!");
                return new ArchiveVideoVideoViewer(context, this, decoder);
            }
            throw new NotImplementedException();
        }
    }

    public class ArchiveVideoVideoViewer : IImageViewer
    {
        DataViewerContext context;
        ArchiveVideoVideo video;
        BufferedVideoDecoder decoder;

        VideoDecoderFrame currentFrame;

        readonly object locker = new object();
        CancellationTokenSource cancellation;

        internal ArchiveVideoVideoViewer(DataViewerContext context, ArchiveVideoVideo video, IVideoDecoder decoder)
        {
            this.context = context;
            this.video = video;
            this.decoder = new BufferedVideoDecoder(decoder);

            this.context.RangeUpdated += (double from, double to) =>
            {
                UpdateCurrentFrame(from);
            };
            UpdateCurrentFrame(this.context.RangeFrom);
        }

        async void UpdateCurrentFrame(double time)
        {
            time = time / 24; // FIXME : Remove when we get a propert slider
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
                Changed?.Invoke();
            }
            catch (OperationCanceledException) { }
        }

        public IDataPoint DataPoint => video;

        public event DataViewWasChangedHandler Changed;

        public double HasDataFrom => throw new NotImplementedException();
        public double HasDataTo => throw new NotImplementedException();
        public event DataViewHasDataRangeChangedHandler HasDataRangeChanged;

        public ImageFrame GetCurrentImage()
        {
            return new ImageFrame(currentFrame.Width, currentFrame.Height, currentFrame.Frame);
        }

        public Task SetSize(uint width, uint height)
        {
            return decoder.SetSizeAsync(width, height);
        }
    }


    /*
    public class ArchiveVideoVideoViewer : IImageViewer
    {
        private DataViewerContext context;
        private ArchiveVideoVideo video;
        private IVideoDecoder decoder;

        int width = 1;
        int height = 1;

        readonly SemaphoreSlim bufferSignal = new SemaphoreSlim(0, 1);
        readonly SemaphoreSlim bufferControl = new SemaphoreSlim(1, 1);
        bool bufferOperationCancel = false;
        bool bufferOperationRunning = false;
        bool bufferStarFresh = false;
        double bufferStartFromTime;
        int bufferRemoveOld;
        int bufferDecodeNew;


        CancellationTokenSource bufferUpdating;
        CancellationTokenSource bufferFilling;

        private LinkedList<(double time, byte[] frame)> frameBuffer = new LinkedList<(double time, byte[] frame)>();
        private LinkedListNode<(double time, byte[] frame)> currentFrameInBuffer;

        internal ArchiveVideoVideoViewer(DataViewerContext context, ArchiveVideoVideo video, IVideoDecoder decoder)
        {
            this.context = context;
            this.video = video;
            this.decoder = decoder;


            // Run the buffer handlin in the background
            Task.Factory.StartNew(StartBufferHandlingAsync);

            // Sync the buffer with the context range
            context.RangeUpdated += (double from, double to) =>
            {
                SetCurrentFrameTime(from);
            };
            SetCurrentFrameTime(context.RangeFrom);
        }

        private async void StartBufferHandlingAsync()
        {
            while (true) // FIXME: Not possible to stop this ever!
            {
                // Wait until we are signaled to do something
                await bufferSignal.WaitAsync();
                bufferOperationRunning = true;

                // Make sure we have control
                await bufferControl.WaitAsync();

                // Figure out what we where supposed to do
                if (bufferStarFresh)
                {
                    // -- Clean the buffer, and then start loading from a given time --
                    lock (frameBuffer)
                    {
                        frameBuffer.Clear();
                    }
                    // Seek to the correct time
                    await decoder.SeekTo(bufferStartFromTime);
                }
                else
                {
                    // -- Remove some old frames, and continue decoding more new frames into the buffer --
                    lock (frameBuffer)
                    {
                        while (bufferRemoveOld > 0)
                        {
                            frameBuffer.RemoveFirst();
                            bufferRemoveOld--;
                        }
                    }
                }

                // Now start decoding new frames
                while (bufferDecodeNew > 0)
                {
                    if (bufferOperationCancel)
                    {
                        break;
                    }
                    var decoded = await decoder.DecodeNextFrame(width, height);
                    lock (frameBuffer)
                    {
                        frameBuffer.AddLast(decoded);
                    }
                    bufferDecodeNew--;
                }

                // Give control back
                bufferOperationRunning = false;
                bufferControl.Release();
            }
        }

        private void SetCurrentFrameTime(double time)
        {
            // FIXME: Remove this, only for debugging
            time = time / 24; //Change to 1 fps

            LinkedListNode<(double time, byte[] frame)> nextFrame = null;
            var framesBefore = 0;
            var framesAfter = 0;

            // Make sure we have the frame closest to that time buffered
            lock (frameBuffer)
            {
                if (frameBuffer.First != null)
                {
                    // There is something in the buffer
                    if (frameBuffer.First.Value.time <= time && frameBuffer.Last.Value.time > time)
                    {
                        // The desired frame should be in the buffer
                        // Find it, and count frames before and after
                        var frame = frameBuffer.First;
                        while (frame != null)
                        {
                            if (frame.Value.time <= time && frame.Next != null && frame.Next.Value.time > time)
                            {
                                // This is it
                                nextFrame = frame;
                            }
                            else
                            {
                                if (nextFrame == null) framesBefore++;
                                else framesAfter++;
                            }
                            frame = frame.Next;
                        }
                    }
                }
            }

            // We should now have found the frame if it is in the buffer, and counted the frames before and after the current one
            if (nextFrame != null)
            {
                // Set the current data
                currentFrameInBuffer = nextFrame;

                // Start potentially updating the buffer
                PotentiallyTriggerBufferUpdate(framesBefore, framesAfter);

                // Trigger the changed event
                Changed?.Invoke();
            }
            else
            {
                // We need to load this frame
                TriggerBufferFillingFrom(time);
            }
        }

        private void PotentiallyTriggerBufferUpdate(int framesBefore, int framesAfter)
        {
            if (framesBefore > 24 || framesAfter < 24)
            {
                if (bufferOperationRunning)
                {
                    // An update is already underway, so let it complete
                    // If needed, this method will be called again later
                    return;
                }
                else
                {
                    // Take control of the buffer
                    bufferControl.Wait();
                    bufferStarFresh = false;
                    bufferRemoveOld = framesBefore;
                    bufferDecodeNew = 24;
                    // Signal to the buffer to do something
                    bufferSignal.Release();
                    // Give control back
                    bufferControl.Release();
                }
            }
        }
        private void TriggerBufferFillingFrom(double time)
        {
            // Cancel whatever the buffer was doing, and start loading frames from 'time'
            bufferOperationCancel = true;
            // Take control of the buffer
            bufferControl.Wait();
            bufferOperationCancel = false;
            bufferStarFresh = true;
            bufferStartFromTime = time;
            bufferDecodeNew = 24;
            // Signal to the buffer to do something
            if (bufferSignal.CurrentCount < 1) bufferSignal.Release();
            // Give control back
            bufferControl.Release();
        }

        public IDataPoint DataPoint => video;

        public async Task SetSize(uint width, uint height)
        {
            await Task.Factory.StartNew(() =>
            {
                // The size has changed, so we need start loading frames from scratch
                bufferOperationCancel = true;
                this.width = (int)width;
                this.height = (int)height;
                // Do the re-filling
                TriggerBufferFillingFrom(context.RangeFrom);
            });
        }

        public event DataViewWasChangedHandler Changed;

        public SpanPair<float> GetCurrentFloat()
        {
            throw new NotImplementedException();
        }

        public byte[] GetPixels()
        {
            if (currentFrameInBuffer != null)
                return currentFrameInBuffer.Value.frame;
            else
                return null;
        }        
    }
    */

        
    [ArchivePlugin("no.sintef.video")]
    public class ArchiveVideoPlugin : IArchivePlugin
    {
        public Task<ArchiveStructure> CreateFromJSON(JObject json, Archive.Archive archive)
        {
            return Task.FromResult<ArchiveStructure>(new ArchiveVideo(json, archive));
        }
    }
}
