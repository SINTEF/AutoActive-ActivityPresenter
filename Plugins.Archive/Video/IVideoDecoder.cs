using System;
using System.Threading;
using System.Threading.Tasks;

using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.ArchivePlugins.Video
{
    public readonly struct VideoDecoderFrame
    {
        public bool Loaded { get; }
        public long Time { get; }
        public uint Width { get; }
        public uint Height { get; }
        public ArraySegment<byte> Frame { get; }

        public VideoDecoderFrame(long time, uint width, uint height, ArraySegment<byte> frame)
        {
            Loaded = true;
            Time = time;
            Width = width;
            Height = height;
            Frame = frame;
        }
    }

    public interface IVideoDecoder
    {
        Task<long> GetLengthAsync();

        Task<long> SeekToAsync(long time, CancellationToken cancellationToken);
        Task<long> SeekToAsync(long time);

        Task<(uint width, uint height)> SetSizeAsync(uint width, uint height, CancellationToken cancellationToken);
        Task<(uint width, uint height)> SetSizeAsync(uint width, uint height);

        Task<VideoDecoderFrame> DecodeNextFrameAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
        Task<VideoDecoderFrame> DecodeNextFrameAsync(ArraySegment<byte> buffer);
    }

    public interface IVideoDecoderFactory
    {
        Task<IVideoDecoder> CreateVideoDecoder(IReadSeekStreamFactory file, string mime);
    }
}
