using SINTEF.AutoActive.Databus.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Databus
{

    public interface IDataReader
    {
        (long[], double[], bool[]) DataAsArrays();
    }

    public class DataReader<T> : IDataReader where T : IConvertible
    {
        private ITimeSeriesViewer Viewer { get; }
        public DataReader(ITimeSeriesViewer viewer)
        {
            Viewer = viewer;
        }

        private SpanPair<T>.Enumerator CreateReader()
        {
            SpanPair<T>.Enumerator en = Viewer.GetCurrentData<T>().GetEnumerator(0);
            return en;
        }

        public (long[], double[], bool[]) DataAsArrays()
        {
            List<long> time = new List<long>();
            List<double> data = new List<double>();
            List<bool> isNaN = new List<bool>();
            SpanPair<T>.Enumerator en = CreateReader();

            if (!en.MoveNext())
            {
                return (time.ToArray(), data.ToArray(), isNaN.ToArray());
            }

            time.Add(en.Current.x);
            data.Add(en.Current.y);
            isNaN.Add(en.Current.isNan);

            while (en.MoveNext())
            {
                time.Add(en.Current.x);
                data.Add(en.Current.y);
                isNaN.Add(en.Current.isNan);
            }
            return (time.ToArray(), data.ToArray(), isNaN.ToArray());
        }

    }
}
