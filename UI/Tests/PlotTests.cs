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
        private void TestDataSet(long[] timeArray, double[] dataArray, int maxNum)
        {
            Assert.Equal(timeArray.Length, dataArray.Length);

            var time = new TableTimeIndex("time", new Task<long[]>(() => timeArray), true);
            var data = new GenericColumn<double>("acc_x", new Task<double[]>(() => dataArray), time);

            var tsc = new TimeSynchronizedContext();
            var dataViewerTask = tsc.GetDataViewerFor(data);
            dataViewerTask.Wait();
            var viewer = dataViewerTask.Result as ITimeSeriesViewer;
            Assert.NotNull(viewer);

            var startTime = timeArray[0];
            Assert.Equal(0, tsc.AvailableTimeFrom);
            Assert.Equal(timeArray.Last() - startTime, tsc.AvailableTimeTo);
            tsc.SetSelectedTimeRange(tsc.AvailableTimeFrom, tsc.AvailableTimeTo);

            var dataView = viewer.GetCurrentData<double>();

            var en = dataView.GetEnumerator(maxNum);

            var count = 0;
            Assert.True(en.MoveNext());
            count++;
            Assert.Equal(timeArray.First(), en.Current.x);
            Assert.Equal(dataArray.First(), en.Current.y);
            while (en.MoveNext())
            {
                count++;
            }
            Assert.Equal(timeArray.Last(), en.Current.x);
            Assert.Equal(dataArray.Last(), en.Current.y);
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
                time[i] = 1000 * i + r.Next(0,750);
            }
            TestDataSet(time, data, maxNum);
        }
        
    }
}
