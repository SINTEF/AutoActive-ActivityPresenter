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


[assembly: Dependency(typeof(VideoDecoderFactory))]
namespace SINTEF.AutoActive.UI.UWP.Video
{
    delegate bool VideoDecoderAction(); // Returns whether the last decoded frame was consumed or not

    public class VideoDecoder : IVideoDecoder
    {
        private readonly MediaPlayer _decoder;
        private SoftwareBitmap _destination;
        private readonly CanvasDevice _device;
        private CanvasBitmap _bitmap;

        private readonly object _mutex = new object();
        private bool _isDecoding;
        private readonly Queue<VideoDecoderAction> _queue = new Queue<VideoDecoderAction>();

        private readonly TaskCompletionSource<long> _videoLength = new TaskCompletionSource<long>();

        public bool DEBUG_OUTPUT = false;
        private bool _sizeChangeRequested;

        internal VideoDecoder(IRandomAccessStream stream, string mime)
        {
            if (DEBUG_OUTPUT) Debug.WriteLine("CREATED DECODER!");

            var source = MediaSource.CreateFromStream(stream, mime);
            var item = new MediaPlaybackItem(source);
            _decoder = new MediaPlayer
            {
                AutoPlay = false,
                IsMuted = true,
                IsVideoFrameServerEnabled = true,
                Source = item,
            };
            _decoder.VideoFrameAvailable += Decoder_VideoFrameAvailable;

            _isDecoding = true;


            _device = new CanvasDevice();
            //CreateBitmaps(10, 10);
        }

        private void RunNextActions()
        {
            //Debug.WriteLine($"RunNextActions #{Thread.CurrentThread.ManagedThreadId}");
            while (_queue.TryDequeue(out var nextAction))
            {
                //Debug.WriteLine($"RunNextAction - Running #{Thread.CurrentThread.ManagedThreadId}");
                // Run the next action in the queue
                if (nextAction())
                {
                    //Debug.WriteLine($"RunNextAction - Starting new decode #{Thread.CurrentThread.ManagedThreadId}");
                    // The previously decoded frame was consumed, so we need to decode the next one
                    _decoder.StepForwardOneFrame();
                    _isDecoding = true;
                }
            }
        }

        private void EnqueueAndPossiblyRun(params VideoDecoderAction[] actions)
        {
            //Debug.WriteLine($"EnqueueAndPossiblyRun #{Thread.CurrentThread.ManagedThreadId}");
            lock (_mutex)
            {
                foreach (var action in actions)
                    _queue.Enqueue(action);

                if (!_isDecoding)
                {
                    // Decoding is already done, so we are free to run the next action here
                    RunNextActions();
                }
            }
        }

        (uint width, uint height) GetActualSize(uint requestedWidth, uint requestedHeight)
        {
            var naturalWidth = (double)_decoder.PlaybackSession.NaturalVideoWidth;
            var naturalHeight = (double)_decoder.PlaybackSession.NaturalVideoHeight;
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

        private void CreateBitmaps(uint width, uint height)
        {
            //Debug.WriteLine($"CreateBitmaps #{Thread.CurrentThread.ManagedThreadId}");
            _destination = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)width, (int)height, BitmapAlphaMode.Ignore);
            _bitmap = CanvasBitmap.CreateFromSoftwareBitmap(_device, _destination);
        }

        VideoDecoderFrame CopyCurrentDecodedFrame(ArraySegment<byte> buffer)
        {
            var time = (long)_decoder.PlaybackSession.Position.TotalMilliseconds * 1000;
            var width = (uint)_destination.PixelWidth;
            var height = (uint)_destination.PixelHeight;
            var slice = buffer.Array.AsBuffer(buffer.Offset, buffer.Count);
            /*destination.CopyToBuffer(slice);*/
            _bitmap.GetPixelBytes(slice);
            return new VideoDecoderFrame(time, width, height, buffer);
        }

        private void Decoder_VideoFrameAvailable(MediaPlayer sender, object args)
        {
            // TODO(sigurdal): Is this supposed to be 1e6 instead of 10e6?
            _videoLength.TrySetResult((long)(_decoder.PlaybackSession.NaturalDuration.TotalSeconds*1e6));
            if (_bitmap != null && !_sizeChangeRequested)
            {
                try
                {
                    _decoder.CopyFrameToVideoSurface(_bitmap);
                }
                catch (ArgumentException ex)
                {
                    Debug.WriteLine($"Could not decode: {ex.Message}");
                }

            }

            lock (_mutex)
            {
                _isDecoding = false;
                RunNextActions();
            }
        }

        /* -- Public API -- */
        public Task<long> GetLengthAsync()
        {
            return _videoLength.Task;
        }

        public Task<VideoDecoderFrame> DecodeNextFrameAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (DEBUG_OUTPUT) Debug.WriteLine($"DecodeNextFrameAsync #{Thread.CurrentThread.ManagedThreadId}");
            TaskCompletionSource<VideoDecoderFrame> source = new TaskCompletionSource<VideoDecoderFrame>();
            EnqueueAndPossiblyRun(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    source.SetCanceled();
                    return false;
                }

                if (buffer.Count >= _destination.PixelHeight*_destination.PixelWidth*4)
                {
                    if (DEBUG_OUTPUT) Debug.WriteLine($"VIDEO FRAME COPIED");
                    // If there is room in the assigned buffer, return the current decoded frame to the caller
                    source.SetResult(CopyCurrentDecodedFrame(buffer));
                    return true;
                }
                else
                {
                    if (DEBUG_OUTPUT) Debug.WriteLine($"VIDEO FRAME SKIPPED");
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
            var source = new TaskCompletionSource<long>();
            EnqueueAndPossiblyRun(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                // First, tell the decoder to seek
                _decoder.PlaybackSession.Position = TimeSpan.FromMilliseconds(time/1000);
                return false;
            }, () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    source.SetCanceled();
                    return false;
                }

                // Then, when the next frame is available, we are done seeking
                var position = (long)(_decoder.PlaybackSession.Position.TotalSeconds * 1e6);
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
            _sizeChangeRequested = true;
            //Debug.WriteLine($"SetSizeAsync {width}x{height} #{Thread.CurrentThread.ManagedThreadId}");
            var source = new TaskCompletionSource<(uint width, uint height)>();
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
                _sizeChangeRequested = false;
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
        private readonly IRandomAccessStream _original;
        private readonly IReadSeekStreamFactory _factory;

        internal FactoryRandomAccessStream(IRandomAccessStream stream, IReadSeekStreamFactory file)
        {
            _original = stream;
            _factory = file;
        }

        public bool CanRead => _original.CanRead;
        public bool CanWrite => _original.CanWrite;
        public ulong Position => _original.Position;

        public void Seek(ulong position) { _original.Seek(position); }
        public ulong Size { get => _original.Size; set => _original.Size = value; }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options) {
            return _original.ReadAsync(buffer, count, options);
        }
        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) { return _original.WriteAsync(buffer); }
        public IAsyncOperation<bool> FlushAsync() { return _original.FlushAsync(); }

        public void Dispose() { _original.Dispose(); }

        private async Task<Stream> GetClonedStream()
        {
            return await _factory.GetReadStream().ConfigureAwait(false);
        }

        public IRandomAccessStream CloneStream()
        {
            var task = Task.Run(_factory.GetReadStream);
            var stream = task.Result;
            return new FactoryRandomAccessStream(stream.AsRandomAccessStream(), _factory);
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
