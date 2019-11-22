using System;
using SINTEF.AutoActive.Plugins.Import.Csv;
using SINTEF.AutoActive.UI.Helpers;
using Xunit;

namespace Plugins.Tests
{
    public class CatapultImporter
    {
        [Fact]
        public void TestParseTimestamp()
        {
            Assert.Equal(0L, CatapultCsvImporter.ConvHmssToEpochUs("0:00.00"));
            Assert.Equal(TimeFormatter.TimeFromTimeSpan(TimeSpan.FromMilliseconds(2290)), CatapultCsvImporter.ConvHmssToEpochUs("0:02.29"));
            Assert.Equal(TimeFormatter.TimeFromTimeSpan(new TimeSpan(0,0,51,50,0)), CatapultCsvImporter.ConvHmssToEpochUs("51:50.00"));
            Assert.Equal(TimeFormatter.TimeFromTimeSpan(new TimeSpan(0, 1, 9, 11, 0)), CatapultCsvImporter.ConvHmssToEpochUs("1:09:11.00"));
        }
    }
}
