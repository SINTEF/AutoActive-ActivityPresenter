using System;
using System.Linq;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.ViewerContext;
using Xunit;

namespace Tests
{
    public class PlotTests
    {
        private static ITimeSeriesViewer CreateTimeSeriesViewer(long[] timeArray, double[] dataArray)
        {
            Assert.Equal(timeArray.Length, dataArray.Length);

            var time = new TableTimeIndex("time", new Task<long[]>(() => timeArray), true, "test:/time", "t");
            var data = new GenericColumn<double>("acc_x", new Task<double[]>(() => dataArray), time, "test/acc", "a");

            var tsc = new TimeSynchronizedContext();
            tsc.SetSynchronizedToWorldClock(false);
            var dataViewerTask = tsc.GetDataViewerFor(data);
            dataViewerTask.Wait();
            var viewer = dataViewerTask.Result as ITimeSeriesViewer;
            Assert.NotNull(viewer);
            viewer.PreviewPercentage = 0;

            var startTime = timeArray[0];
            Assert.Equal(0, tsc.AvailableTimeFrom);
            Assert.Equal(timeArray.Last() - startTime, tsc.AvailableTimeTo);
            tsc.SetSelectedTimeRange(tsc.AvailableTimeFrom, tsc.AvailableTimeTo);
            return viewer;
        }

        private void TestDataSet(long[] timeArray, double[] dataArray, int maxNum)
        {
            var viewer = CreateTimeSeriesViewer(timeArray, dataArray);

            var dataView = viewer.GetCurrentData<double>();

            var en = dataView.GetEnumerator(maxNum);

            var count = 0;
            Assert.True(en.MoveNext());
            count++;

            Assert.Equal((double)timeArray.First(), en.Current.x, 5);
            Assert.Equal(dataArray.First(), en.Current.y, 5);
            while (en.MoveNext())
            {
                count++;
            }
            Assert.Equal((double)timeArray.Last(), en.Current.x, 5);
            Assert.Equal(dataArray.Last(), en.Current.y, 5);
            if (maxNum > 0)
            {
                Assert.InRange(count, maxNum, maxNum + 2);
            }
            else
            {
                Assert.Equal(count, timeArray.Length);
            }
        }

        [Fact]
        public void EnumeratorDecimatorTest()
        {
            var timeArray = new long[] {0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100};
            var dataArray = new double[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
            const int maxNum = 10;
            TestDataSet(timeArray, dataArray, maxNum);
        }

        [Theory]
        [InlineData(20, 10)]
        [InlineData(20, 2)]
        [InlineData(20, -1)]
        [InlineData(3000, 333)]
        [InlineData(1000, 23)]
        public void RandomDataGenerator(uint size, int maxNum)
        {
            var r = new Random();
            var data = new double[size];
            var time = new long[size];
            for (var i = 0; i < size; i++)
            {
                data[i] = r.NextDouble();
                time[i] = 1000 * i + r.Next(0, 750);
            }
            TestDataSet(time, data, maxNum);
        }

        [Fact]
        public void CheckAllData()
        {
            var timeArray = new long[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            var dataArray = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            var viewer = CreateTimeSeriesViewer(timeArray, dataArray) as TableColumnViewer;
            viewer.SetTimeRange(timeArray.First(), timeArray.Last());
            var data = viewer.GetCurrentData<double>();
            var en = data.GetEnumerator(dataArray.Length*10);

            var index = 0;
            while(en.MoveNext())
            {
                Assert.Equal(timeArray[index], en.Current.x);
                Assert.Equal(dataArray[index], en.Current.y);
                index++;
            }
        }

        [Fact]
        public void TestCurrentDataSelector()
        {
            var timeArray = new long[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            var dataArray = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            var viewer = CreateTimeSeriesViewer(timeArray, dataArray) as TableColumnViewer;
            viewer.SetTimeRange(20, 40);
            //viewer.StartIndex = 1;
            var data = viewer.GetCurrentData<double>();
            var en = data.GetEnumerator(100);

            Assert.True(en.MoveNext());
            Assert.Equal(20, en.Current.x);
            Assert.True(en.MoveNext());
            Assert.Equal(30, en.Current.x);
            Assert.True(en.MoveNext());
            Assert.Equal(40, en.Current.x);
            //Assert.False(en.MoveNext());
        }

        [Fact]
        public void ModuloEnumeratorTest()
        {
            var size = 1000;

            var dataArray = new double[size];
            var timeArray = new long[size];
            for (var i = 0; i < size; i++)
            {
                timeArray[i] = i;
                dataArray[i] = i;
            }

            var viewer = CreateTimeSeriesViewer(timeArray, dataArray) as TableColumnViewer;
            var startTime = 3;
            var maxNum = 10;
            var endTime = 103;
            var step = (endTime - startTime) / maxNum;
            viewer.SetTimeRange(startTime, endTime);
            //viewer.StartIndex = 1;
            var data = viewer.GetCurrentData<double>();

            var en = data.GetEnumerator(maxNum);

            Assert.True(en.MoveNext());

            var prev = en.Current.x;
            Assert.Equal(step, en.Current.x);

            for (var expected=step*2; expected <= endTime; expected+=step)
            {
                Assert.True(en.MoveNext());
                Assert.Equal(expected, en.Current.x);
            }
        }

    }
}
