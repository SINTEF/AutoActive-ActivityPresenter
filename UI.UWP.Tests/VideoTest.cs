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
        private static VideoLengthExtractor GetDecoder(string path = "TestData/small.mp4")
        {
            var mime = MimeUtility.GetMimeMapping(path);
            var videoStream = File.OpenRead(path);
            var decoder = new VideoLengthExtractor(videoStream.AsRandomAccessStream(), mime);
            return decoder;
        }

        [Fact]
        public async void SimpleDecoding()
        {
            var decoder = GetDecoder();
            var videoLength = await decoder.GetLengthAsync();

            //This video should be about 5 seconds long
            Assert.InRange(videoLength, TimeFormatter.TimeFromSeconds(4), TimeFormatter.TimeFromSeconds(6));
        }

    }
}