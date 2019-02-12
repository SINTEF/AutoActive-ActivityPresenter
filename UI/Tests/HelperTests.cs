using System;
using Xunit;
using SINTEF.AutoActive.UI;
using SINTEF.AutoActive.UI.Helpers;

namespace SINTEF.AutoActive.Tests
{
    public class HelperTests
    {
        private const long microsPerSec = 1000000;

        [Fact]
        public void TimeFormattingTest()
        {
            var oneSecond = microsPerSec;
            Assert.Equal("00:00:01.000", TimeFormatter.FormatTime(oneSecond));

            var oneMinute = 60L * oneSecond;
            Assert.Equal("00:01:00.000", TimeFormatter.FormatTime(oneMinute));

            var oneHour = 60L * oneMinute;
            Assert.Equal("01:00:00.000", TimeFormatter.FormatTime(oneHour));

            var oneHourOneMinuteOneSecond = oneHour + oneMinute + oneSecond;
            Assert.Equal("01:01:01.000", TimeFormatter.FormatTime(oneHourOneMinuteOneSecond));

            Assert.Equal("00:00:00.100", TimeFormatter.FormatTime(100000));
            Assert.Equal("00:00:00.111", TimeFormatter.FormatTime(111000));
            Assert.Equal("00:00:00.000", TimeFormatter.FormatTime(000100));

            Assert.Equal("300:00:00.000", TimeFormatter.FormatTime(oneHour*300));
        }
    }
}
