using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.ArchivePlugins.Video
{
    public interface IVideoDecoder
    {
        Task<double> GetLengthAsync();
        Task<(double time, byte[] frame)> DecodeNextFrame(int width, int height);
        Task<double> SeekTo(double time);

        /*
        double Length { get; }
        (double time, byte[] frame) CurrentFrame { get; }

        bool Loaded { get;  }

        Task Load(uint width, uint height);
        Task SetOutputFrameSize(uint width, uint height);
        Task SeekTo(double time);
        Task DecodeNextFrame();
        */
    }

    public interface IVideoDecoderFactory
    {
        Task<IVideoDecoder> CreateVideoDecoder(IReadSeekStreamFactory file, string mime);
    }
}
