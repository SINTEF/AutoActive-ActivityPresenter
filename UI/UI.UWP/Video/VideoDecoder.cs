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
        CanvasBitmap bitmap;

        readonly object locker = new object();
        bool isDecoding;
        readonly Queue<VideoDecoderAction> queue;

        TaskCompletionSource<double> length;

        internal VideoDecoder(IRandomAccessStream stream, string mime)
        {
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

            length = new TaskCompletionSource<double>();

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

        void CreateBitmaps(uint width, uint height)
        {
            //Debug.WriteLine($"CreateBitmaps #{Thread.CurrentThread.ManagedThreadId}");
            destination = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)width, (int)height, BitmapAlphaMode.Ignore);
            bitmap = CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice.GetSharedDevice(), destination);
        }

        VideoDecoderFrame CopyCurrentDecodedFrame()
        {
            //Debug.WriteLine($"CopyCurrentDecodedFrame #{Thread.CurrentThread.ManagedThreadId}");
            var time = decoder.PlaybackSession.Position.TotalSeconds;
            var width = (uint)destination.PixelWidth;
            var height = (uint)destination.PixelHeight;
            var frame = bitmap.GetPixelBytes();
            return new VideoDecoderFrame(time, width, height, frame);
        }

        void Decoder_VideoFrameAvailable(MediaPlayer sender, object args)
        {
            //Debug.WriteLine($"VideoFrameAvailable time:{decoder.PlaybackSession.Position.TotalSeconds} #{Thread.CurrentThread.ManagedThreadId}");
            length.TrySetResult(decoder.PlaybackSession.NaturalDuration.TotalSeconds);

            decoder.CopyFrameToVideoSurface(bitmap);

            lock (locker)
            {
                isDecoding = false;
                RunNextActions();
            }
        }

        /* -- Public API -- */
        public Task<double> GetLengthAsync()
        {
            throw new NotImplementedException();
        }

        public Task<VideoDecoderFrame> DecodeNextFrameAsync(CancellationToken cancellationToken)
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

                // Return the current decoded frame to the caller
                source.SetResult(CopyCurrentDecodedFrame());
                return true;
            });
            return source.Task;
        }
        public Task<VideoDecoderFrame> DecodeNextFrameAsync()
        {
            return DecodeNextFrameAsync(CancellationToken.None);
        }

        public Task<double> SeekToAsync(double time, CancellationToken cancellationToken)
        {
            //Debug.WriteLine($"SeekToAsync {Thread.CurrentThread.ManagedThreadId}");
            TaskCompletionSource<double> source = new TaskCompletionSource<double>();
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
                source.SetResult(decoder.PlaybackSession.Position.TotalSeconds);
                return false;
            });
            return source.Task;
        }
        public Task<double> SeekToAsync(double time)
        {
            return SeekToAsync(time, CancellationToken.None);
        }

        public Task SetSizeAsync(uint width, uint height, CancellationToken cancellationToken)
        {
            //Debug.WriteLine($"SetSizeAsync {width}x{height} #{Thread.CurrentThread.ManagedThreadId}");
            TaskCompletionSource<object> source = new TaskCompletionSource<object>();
            EnqueueAndPossiblyRun(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    source.SetCanceled();
                    return false;
                }

                // Just create new bitmaps
                CreateBitmaps(width, height);
                source.SetResult(null);
                return false;
            });
            return source.Task;
        }
        public Task SetSizeAsync(uint width, uint height)
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
