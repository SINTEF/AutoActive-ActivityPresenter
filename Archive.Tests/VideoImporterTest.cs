using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SINTEF.AutoActive.Plugins.Import.Video;
using Xunit;

namespace SINTEF.AutoActive.Archive.Tests
{
    public class VideoImporterTest
    {
        [Fact]
        public void StringParserTest()
        {
            var sub1_2_MOV_Created = "Mon Jun 03 14:51:56 2019";

            var expectedDate = new DateTime(2019,6,3,14,51,56);

            var culture = CultureInfo.GetCultureInfo("en-US");
            var str = expectedDate.ToString("ddd MMM dd HH:mm:ss yyyy", culture);


            Assert.True(VideoImporter.TryParseDateTime(sub1_2_MOV_Created, out DateTime date));
            Assert.Equal(expectedDate, date);

            Assert.Equal(2019, date.Year);
            Assert.Equal(6, date.Month);
            Assert.Equal(3, date.Day);
            Assert.Equal(14, date.Hour);
            Assert.Equal(51, date.Minute);
            Assert.Equal(56, date.Second);
        }
    }
}
