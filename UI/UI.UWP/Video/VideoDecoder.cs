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

[assembly: Dependency(typeof(VideoDecoderFactory))]
namespace SINTEF.AutoActive.UI.UWP.Video
{
    public class VideoDecoder : IVideoDecoder
    {
        MediaPlayer decoder;
        SoftwareBitmap destination;
        CanvasBitmap bitmap;

        TaskCompletionSource<double> length = new TaskCompletionSource<double>();
        TaskCompletionSource<double> seeking = new TaskCompletionSource<double>();
        TaskCompletionSource<object> decoding = new TaskCompletionSource<object>();

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
            decoder.SeekCompleted += Decoder_SeekCompleted;
        }

        private void Decoder_SeekCompleted(MediaPlayer sender, object args)
        {
            seeking.TrySetResult(decoder.PlaybackSession.Position.TotalSeconds);
        }

        private void CreateBitmaps(int width, int height)
        {
            destination = new SoftwareBitmap(BitmapPixelFormat.Rgba8, width, height, BitmapAlphaMode.Ignore);
            bitmap = CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice.GetSharedDevice(), destination);
        }

        private void Decoder_VideoFrameAvailable(MediaPlayer sender, object args)
        {
            // Set initial loading video data
            length.TrySetResult(decoder.PlaybackSession.NaturalDuration.TotalSeconds);

            // We need to copy the frame somewhere for the decoder to stop calling this method, so if we don't have a size yet, use the natural video size
            if (bitmap == null)
            {
                CreateBitmaps((int)decoder.PlaybackSession.NaturalVideoWidth, (int)decoder.PlaybackSession.NaturalVideoHeight);
            }

            // Copy the decoded frame
            decoder.CopyFrameToVideoSurface(bitmap);
            if (!decoding.TrySetResult(null))
            {
                // FIXME : if this appears, probably has something to do with seeking
                Debug.WriteLine("ERROR: Frame available when already decoded!");
            }
        }

        public async Task<(double time, byte[] frame)> DecodeNextFrame(int width, int height)
        {
            // Wait for decoding to complete
            await decoding.Task;

            // Check that the frame buffer is the right size
            if (destination.PixelWidth != width || destination.PixelHeight != height)
            {
                CreateBitmaps(width, height);
                decoder.CopyFrameToVideoSurface(bitmap);
            }

            // Get the frame time and pixels
            var time = decoder.PlaybackSession.Position.TotalSeconds;
            var frame = bitmap.GetPixelBytes();

            // Start decoding the next frame
            decoding = new TaskCompletionSource<object>();
            decoder.StepForwardOneFrame();

            return (time, frame);
        }

        public Task<double> GetLengthAsync()
        {
            return length.Task;
        }

        public async Task<double> SeekTo(double time)
        {
            // Wait for a decoding to complete
            await decoding.Task;

            // Start the seeking
            seeking = new TaskCompletionSource<double>();
            decoder.PlaybackSession.Position = TimeSpan.FromSeconds(time);

            // Wait for the seeking to finish
            return await seeking.Task;
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
