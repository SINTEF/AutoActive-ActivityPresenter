using SINTEF.AutoActive.UI.UWP.Video;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using System.Diagnostics;
using System.IO;
using Windows.Storage.Streams;
using Windows.Media.Playback;
using Windows.Media.Core;
using Windows.Graphics.Imaging;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Graphics.Display;
using SINTEF.AutoActive.FileSystem;
using Windows.Foundation;

using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks.Schedulers;
using System.Collections.Concurrent;

[assembly: Dependency(typeof(VideoDecoderFactory))]
namespace SINTEF.AutoActive.UI.UWP.Video
{
    delegate bool VideoDecoderAction(); // Returns whether the last decoded frame was consumed or not

    public class VideoDecoder : IVideoDecoder
    {
        MediaPlayer decoder;
        SoftwareBitmap destination;
        CanvasDevice device;
        CanvasBitmap bitmap;

        readonly object locker = new object();
        bool isDecoding;
        readonly Queue<VideoDecoderAction> queue;

        TaskCompletionSource<long> length;

        internal VideoDecoder(IRandomAccessStream stream, string mime)
        {
            Debug.WriteLine("CREATED DECODER!");

            var source = MediaSource.CreateFromStream(stream, mime);
            var item = new MediaPlaybackItem(source);
            decoder = new MediaPlayer
            {
                AutoPlay = false,
                IsMuted = true,
                IsVideoFrameServerEnabled = true,
                Source = item,
            };
            decoder.VideoFrameAvailable += Decoder_VideoFrameAvailable;

            isDecoding = true;
            queue = new Queue<VideoDecoderAction>();

            length = new TaskCompletionSource<long>();

            device = new CanvasDevice();
            CreateBitmaps(1, 1);
        }

        void RunNextActions()
        {
            //Debug.WriteLine($"RunNextActions #{Thread.CurrentThread.ManagedThreadId}");
            while (queue.TryDequeue(out var nextAction))
            {
                //Debug.WriteLine($"RunNextAction - Running #{Thread.CurrentThread.ManagedThreadId}");
                // Run the next action in the queue
                if (nextAction())
                {
                    //Debug.WriteLine($"RunNextAction - Starting new decode #{Thread.CurrentThread.ManagedThreadId}");
                    // The previously decoded frame was consumed, so we need to decode the next one
                    decoder.StepForwardOneFrame();
                    isDecoding = true;
                }
            }
        }

        void EnqueueAndPossiblyRun(params VideoDecoderAction[] actions)
        {
            //Debug.WriteLine($"EnqueueAndPossiblyRun #{Thread.CurrentThread.ManagedThreadId}");
            lock (locker)
            {
                foreach (var action in actions)
                    queue.Enqueue(action);

                if (!isDecoding)
                {
                    // Decoding is already done, so we are free to run the next action here
                    RunNextActions();
                }
            }
        }

        (uint width, uint height) GetActualSize(uint requestedWidth, uint requestedHeight)
        {
            var naturalWidth = (double)decoder.PlaybackSession.NaturalVideoWidth;
            var naturalHeight = (double)decoder.PlaybackSession.NaturalVideoHeight;
            var widthFactor = requestedWidth / naturalWidth;
            var heightFactor = requestedHeight / naturalHeight;
            if (widthFactor < heightFactor)
            {
                return (requestedWidth, (uint)(naturalHeight * widthFactor));
            }
            else
            {
                return ((uint)(naturalWidth * heightFactor), requestedHeight);
            }
        }

        void CreateBitmaps(uint width, uint height)
        {
            //Debug.WriteLine($"CreateBitmaps #{Thread.CurrentThread.ManagedThreadId}");
            destination = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)width, (int)height, BitmapAlphaMode.Ignore);
            bitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, destination);
        }

        VideoDecoderFrame CopyCurrentDecodedFrame(ArraySegment<byte> buffer)
        {
            //Debug.WriteLine($"CopyCurrentDecodedFrame #{Thread.CurrentThread.ManagedThreadId}");
            var time = (long)(decoder.PlaybackSession.Position.TotalSeconds*10e6);
            var width = (uint)destination.PixelWidth;
            var height = (uint)destination.PixelHeight;
            var slice = buffer.Array.AsBuffer(buffer.Offset, buffer.Count);
            /*destination.CopyToBuffer(slice);*/
            bitmap.GetPixelBytes(slice);
            return new VideoDecoderFrame(time, width, height, buffer);
        }

        void Decoder_VideoFrameAvailable(MediaPlayer sender, object args)
        {
            Debug.WriteLine($"VideoFrameAvailable time:{decoder.PlaybackSession.Position.TotalSeconds} #{Thread.CurrentThread.ManagedThreadId}");
            length.TrySetResult((long)(decoder.PlaybackSession.NaturalDuration.TotalSeconds*10e6));

            decoder.CopyFrameToVideoSurface(bitmap);

            lock (locker)
            {
                isDecoding = false;
                RunNextActions();
            }
        }

        /* -- Public API -- */
        public Task<long> GetLengthAsync()
        {
            return length.Task;
        }

        public Task<VideoDecoderFrame> DecodeNextFrameAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            //Debug.WriteLine($"DecodeNextFrameAsync #{Thread.CurrentThread.ManagedThreadId}");
            TaskCompletionSource<VideoDecoderFrame> source = new TaskCompletionSource<VideoDecoderFrame>();
            EnqueueAndPossiblyRun(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    source.SetCanceled();
                    return false;
                }

                if (buffer.Count >= destination.PixelHeight*destination.PixelWidth*4)
                {
                    Debug.WriteLine($"VIDEO FRAME COPIED");
                    // If there is room in the assigned buffer, return the current decoded frame to the caller
                    source.SetResult(CopyCurrentDecodedFrame(buffer));
                    return true;
                }
                else
                {
                    Debug.WriteLine($"VIDEO FRAME SKIPPED");
                    // If not, return a not decoded frame, and leave the current data for later
                    source.SetResult(new VideoDecoderFrame());
                    return false;
                }
                
            });
            return source.Task;
        }
        public Task<VideoDecoderFrame> DecodeNextFrameAsync(ArraySegment<byte> buffer)
        {
            return DecodeNextFrameAsync(buffer, CancellationToken.None);
        }

        public Task<long> SeekToAsync(long time, CancellationToken cancellationToken)
        {
            //Debug.WriteLine($"SeekToAsync {Thread.CurrentThread.ManagedThreadId}");
            TaskCompletionSource<long> source = new TaskCompletionSource<long>();
            EnqueueAndPossiblyRun(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                // First, tell the decoder to seek
                decoder.PlaybackSession.Position = TimeSpan.FromSeconds(time);
                return false;
            }, () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    source.SetCanceled();
                    return false;
                }

                // Then, when the next frame is available, we are done seeking
                var position = (long)(decoder.PlaybackSession.Position.TotalSeconds * 10e6);
                source.SetResult(position);
                return false;
            });
            return source.Task;
        }
        public Task<long> SeekToAsync(long time)
        {
            return SeekToAsync(time, CancellationToken.None);
        }

        public Task<(uint width, uint height)> SetSizeAsync(uint width, uint height, CancellationToken cancellationToken)
        {
            //Debug.WriteLine($"SetSizeAsync {width}x{height} #{Thread.CurrentThread.ManagedThreadId}");
            TaskCompletionSource<(uint width, uint height)> source = new TaskCompletionSource<(uint width, uint height)>();
            EnqueueAndPossiblyRun(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    source.SetCanceled();
                    return false;
                }

                // Calculate appropriate size
                var (actualWidth, actualHeight) = GetActualSize(width, height);

                // Just create new bitmaps
                CreateBitmaps(actualWidth, actualHeight);
                source.SetResult((actualWidth, actualHeight));
                return false;
            });
            return source.Task;
        }
        public Task<(uint width, uint height)> SetSizeAsync(uint width, uint height)
        {
            return SetSizeAsync(width, height, CancellationToken.None);
        }
    }

    public class VideoDecoderFactory : IVideoDecoderFactory
    {
        public async Task<IVideoDecoder> CreateVideoDecoder(IReadSeekStreamFactory file, string mime)
        {
            var stream = await file.GetReadStream();
            return new VideoDecoder(new FactoryRandomAccessStream(stream.AsRandomAccessStream(), file), mime);
        }
    }
    

    /* --- IRandomAccessStream helper --- */
    internal class FactoryRandomAccessStream : IRandomAccessStream
    {
        IRandomAccessStream original;
        IReadSeekStreamFactory factory;

        internal FactoryRandomAccessStream(IRandomAccessStream stream, IReadSeekStreamFactory file)
        {
            original = stream;
            factory = file;
        }

        public bool CanRead => original.CanRead;
        public bool CanWrite => original.CanWrite;
        public ulong Position => original.Position;

        public void Seek(ulong position) { original.Seek(position); }
        public ulong Size { get => original.Size; set => original.Size = value; }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options) {
            return original.ReadAsync(buffer, count, options);
        }
        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) { return original.WriteAsync(buffer); }
        public IAsyncOperation<bool> FlushAsync() { return original.FlushAsync(); }

        public void Dispose() { original.Dispose(); }

        private async Task<Stream> GetClonedStream()
        {
            return await factory.GetReadStream().ConfigureAwait(false);
        }

        public IRandomAccessStream CloneStream()
        {
            var task = Task.Run(factory.GetReadStream);
            var stream = task.Result;
            return new FactoryRandomAccessStream(stream.AsRandomAccessStream(), factory);
        }

        public IInputStream GetInputStreamAt(ulong position)
        {
            var stream = CloneStream();
            stream.Seek(position);
            return stream;
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            var stream = CloneStream();
            stream.Seek(position);
            return stream;
        }
    }
}
