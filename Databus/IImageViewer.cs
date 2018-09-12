using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Databus
{
    public readonly struct ImageFrame
    {
        public uint Width { get; }
        public uint Height { get; }
        public ArraySegment<byte> Frame { get; }

        public ImageFrame(uint width, uint height, ArraySegment<byte> frame)
        {
            Width = width;
            Height = height;
            Frame = frame;
        }
    }

    public interface IImageViewer : IDataViewer
    {
        Task SetSize(uint width, uint height);

        ImageFrame GetImage();
    }
}
