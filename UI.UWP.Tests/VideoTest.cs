using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using MimeMapping;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.UWP.Video;

namespace UI.UWP.Tests
{

    public class VideoTest
    {
        private static VideoDecoder GetDecoder(string path = "TestData/small.mp4")
        {
            var mime = MimeUtility.GetMimeMapping(path);
            var videoStream = File.OpenRead(path);
            var decoder = new VideoDecoder(videoStream.AsRandomAccessStream(), mime);
            return decoder;
        }

        private static async Task SizeCheck(VideoDecoder decoder, uint width, uint height, uint expectedWidth,
            uint expectedHeight)
        {
            var (actualWidth, actualHeight) = await decoder.SetSizeAsync(width, height);
            Assert.Equal(expectedWidth, actualWidth);
            Assert.Equal(expectedHeight, actualHeight);

            var buffer = new ArraySegment<byte>(new byte[actualHeight * actualWidth * 4]);
            await decoder.SeekToAsync(0);
            var frame = await decoder.DecodeNextFrameAsync(buffer);
            Assert.Equal(expectedWidth, frame.Width);
            Assert.Equal(expectedHeight, frame.Height);
        }

        [Fact]
        public async void SimpleDecoding()
        {
            var decoder = GetDecoder();
            var videoLength = await decoder.GetLengthAsync();

            //This video should be about 5 seconds long
            Assert.InRange(videoLength, TimeFormatter.TimeFromSeconds(4), TimeFormatter.TimeFromSeconds(6));

            var (width, height) = decoder.GetVideoSize();
            Assert.Equal(560U, width);
            Assert.Equal(320U, height);

            await SizeCheck(decoder, width, height, width, height);
        }

        [Fact]
        public async void ResizingDecoding()
        {
            var decoder = GetDecoder();

            // Wait for decoder to decode one frame
            await decoder.GetLengthAsync();

            var (width, height) = decoder.GetVideoSize();
            Assert.Equal(560U, width);
            Assert.Equal(320U, height);

            await SizeCheck(decoder, 56U, height, 56U, 32U);

            await SizeCheck(decoder, width, 32U, 56U, 32U);

            await SizeCheck(decoder, width, height, width, height);
        }

    }
}