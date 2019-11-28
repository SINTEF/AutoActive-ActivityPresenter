using System;
using System.Globalization;
using System.IO;
using SINTEF.AutoActive.Plugins.Import.Csv;
using Xunit;

namespace Plugins.Tests
{
    public class CsvImporterTest
    {
        [Fact]
        public void TestSimpleTypeExtraction()
        {
            Assert.Equal(typeof(long), GenericCsvParser.TryGuessTypeSingle("1337", CultureInfo.InvariantCulture));
            Assert.Equal(typeof(string), GenericCsvParser.TryGuessTypeSingle("Test", CultureInfo.InvariantCulture));
            Assert.Equal(typeof(double), GenericCsvParser.TryGuessTypeSingle("3.14", CultureInfo.InvariantCulture));
            Assert.Equal(typeof(DateTime), GenericCsvParser.TryGuessTypeSingle("2014-12-24 01:02:03", CultureInfo.InvariantCulture));

            using (var stream = new FileStream("testdata/csv/123.csv", FileMode.Open))
            {
                var types = GenericCsvParser.TryGuessType(stream);
                Assert.Equal(typeof(double), types[0]);
                Assert.Equal(typeof(long), types[1]);
            }

            using (var stream = new FileStream("testdata/csv/changing_type.csv", FileMode.Open))
            {
                var types = GenericCsvParser.TryGuessType(stream);
                Assert.Equal(typeof(long), types[0]);
                Assert.Equal(typeof(double), types[1]);
            }

            using (var stream = new FileStream("testdata/csv/changing_type_late.csv", FileMode.Open))
            {
                var types = GenericCsvParser.TryGuessType(stream);
                Assert.Equal(typeof(long), types[0]);
                Assert.Equal(typeof(double), types[1]);
            }
        }

        [Fact]
        public void TestParseSimpleCsv()
        {
            using (var stream = new FileStream("testdata/csv/123.csv", FileMode.Open))
            {
                var (headers, types, data) = GenericCsvParser.Parse(stream);
                Assert.Equal("time", headers[0]);
                Assert.Equal("count", headers[1]);

                var time = data[0];
                var currTime = 1d;
                for (var i = 0; i < time.Length; i++)
                {
                    Assert.Equal(currTime, time.GetValue(i));
                    currTime += 0.5d;
                }

                var count = data[1];
                var currData = 0L;
                for (var i = 0; i < count.Length; i++)
                {
                    Assert.Equal(currData, count.GetValue(i));
                    currData++;
                }
            }

            using (var stream = new FileStream("testdata/csv/excel_comma.csv", FileMode.Open))
            {
                var (headers, types, data) = GenericCsvParser.Parse(stream);
                Assert.Equal("counter", headers[0]);
                Assert.Equal("double_and_half", headers[1]);

                Assert.Equal(typeof(long), types[0]);
                Assert.Equal(typeof(double), types[1]);

                var time = data[0];
                var count = data[1];
                var currTime = 1L;
                for (var i = 0; i < time.Length; i++)
                {
                    Assert.Equal(currTime, time.GetValue(i));
                    Assert.Equal(currTime * 2.5d, count.GetValue(i));

                    currTime += 1;
                }
            }

            using (var stream = new FileStream("testdata/csv/hardnames.csv", FileMode.Open))
            {
                var (headers, types, data) = GenericCsvParser.Parse(stream);
                Assert.Equal("epoch's", headers[0]);
                Assert.Equal("the double", headers[1]);
                Assert.Equal("speed [m/s]", headers[2]);

                Assert.Equal(typeof(long), types[0]);
                Assert.Equal(typeof(long), types[1]);
                Assert.Equal(typeof(double), types[2]);

                var currTime = 1L;
                for (var i = 0; i < data[0].Length; i++)
                {
                    Assert.Equal(currTime, data[0].GetValue(i));
                    Assert.Equal(currTime * 2, data[1].GetValue(i));
                    Assert.Equal(currTime * Math.PI, data[2].GetValue(i));

                    currTime += 1;
                }
            }

            using (var stream = new FileStream("testdata/csv/withindex.csv", FileMode.Open))
            {
                var (headers, types, data) = GenericCsvParser.Parse(stream);
                Assert.Equal("", headers[0]);
                Assert.Equal("counter", headers[1]);
                Assert.Equal("double", headers[2]);

                Assert.Equal(typeof(long), types[0]);
                Assert.Equal(typeof(long), types[1]);
                Assert.Equal(typeof(long), types[2]);

                var currTime = 0L;
                for (var i = 0; i < data[0].Length; i++)
                {
                    Assert.Equal(currTime, data[0].GetValue(i));
                    currTime += 1;
                    Assert.Equal(currTime, data[1].GetValue(i));
                    Assert.Equal(currTime * 2, data[2].GetValue(i));
                }
            }

            using (var stream = new FileStream("testdata/csv/world_time.csv", FileMode.Open))
            {
                var (headers, types, data) = GenericCsvParser.Parse(stream);
                Assert.Equal(typeof(DateTime), types[0]);
                Assert.Equal(typeof(long), types[1]);
                Assert.Equal(typeof(long), types[2]);

                var dateTime = new DateTime(2019, 11, 20, 13, 37, 0);
                foreach (var dt in data[0])
                {
                    Assert.Equal(dateTime, dt);
                    dateTime = dateTime.AddSeconds(1);
                }
            }

            using (var stream = new FileStream("testdata/csv/custom_header.csv", FileMode.Open))
            {
                var (headers, types, data) = GenericCsvParser.Parse(stream);
                Assert.Equal("time", headers[0]);
                Assert.Equal("count", headers[1]);

                Assert.Equal(typeof(double), types[0]);
                Assert.Equal(typeof(long), types[1]);

                var currTime = 1d;
                var count = 0L;
                for (var i = 0; i < data[0].Length; i++)
                {
                    Assert.Equal(currTime, data[0].GetValue(i));
                    Assert.Equal(count++, data[1].GetValue(i));
                    currTime += 0.5d;
                }
            }

            using (var stream = new FileStream("testdata/csv/custom_header_linux_ending.csv", FileMode.Open))
            {
                var (headers, types, data) = GenericCsvParser.Parse(stream);
                Assert.Equal("time", headers[0]);
                Assert.Equal("count", headers[1]);

                Assert.Equal(typeof(double), types[0]);
                Assert.Equal(typeof(long), types[1]);

                var currTime = 1d;
                var count = 0L;
                for (var i = 0; i < data[0].Length; i++)
                {
                    Assert.Equal(currTime, data[0].GetValue(i));
                    Assert.Equal(count++, data[1].GetValue(i));
                    currTime += 0.5d;
                }
            }
        }

        [Fact]
        public void TestParseCatapultCsv()
        {
            using (var stream = new FileStream("testdata/csv/catapult_export.csv", FileMode.Open))
            {
                var (headers, types, data) = GenericCsvParser.Parse(stream);
                Assert.Equal("Time", headers[0]);
                Assert.Equal("Forward", headers[1]);
                Assert.Equal("Sideways", headers[2]);
                Assert.Equal("Up", headers[3]);
                Assert.Equal(21, headers.Count);

                Assert.Equal(typeof(string), types[0]);
                for (int i = 1; i < 14; i++)
                {
                    Assert.Equal(typeof(double), types[i]);
                }
                Assert.Equal(typeof(long), types[14]);
                for (int i = 15; i < 17; i++)
                {
                    Assert.Equal(typeof(double), types[i]);
                }

                Assert.Equal(2, data[0].Length);
            }

            using (var stream = new FileStream("testdata/csv/catapult_with_data.csv", FileMode.Open))
            {
                var (headers, types, data) = GenericCsvParser.Parse(stream);
                Assert.Equal("Time", headers[0]);
                Assert.Equal("Forward", headers[1]);
                Assert.Equal("Sideways", headers[2]);
                Assert.Equal("Up", headers[3]);
                Assert.Equal(21, headers.Count);

                Assert.Equal(typeof(string), types[0]);
                for (int i = 1; i < 14; i++)
                {
                    Assert.Equal(typeof(double), types[i]);
                }
                Assert.Equal(typeof(long), types[14]);
                for (int i = 15; i < 17; i++)
                {
                    Assert.Equal(typeof(double), types[i]);
                }

                Assert.Equal(200, data[0].Length);
            }
        }
    }
}
