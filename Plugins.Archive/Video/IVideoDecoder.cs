using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.ArchivePlugins.Video
{
    public readonly struct VideoDecoderFrame
    {
        public double Time { get; }
        public uint Width { get; }
        public uint Height { get; }
        public byte[] Frame { get; }

        public VideoDecoderFrame(double time, uint width, uint height, byte[] frame)
        {
            Time = time;
            Width = width;
            Height = height;
            Frame = frame;
        }
    }

    public interface IVideoDecoder
    {
        Task<double> GetLengthAsync();

        Task<double> SeekToAsync(double time, CancellationToken cancellationToken);
        Task<double> SeekToAsync(double time);

        Task SetSizeAsync(uint width, uint height, CancellationToken cancellationToken);
        Task SetSizeAsync(uint width, uint height);

        Task<VideoDecoderFrame> DecodeNextFrameAsync(CancellationToken cancellationToken);
        Task<VideoDecoderFrame> DecodeNextFrameAsync();
    }

    public interface IVideoDecoderFactory
    {
        Task<IVideoDecoder> CreateVideoDecoder(IReadSeekStreamFactory file, string mime);
    }
}
