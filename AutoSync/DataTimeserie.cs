using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus;
using static SINTEF.AutoActive.AutoSync.AutoSyncUtils;

namespace SINTEF.AutoActive.AutoSync
{
    class Timeserie
    {
        private List<Signal> _data = new List<Signal>();
        private Timeline _time;

        public List<Signal> Data
        {
            get => _data;
        }

        public Timeline Time
        {
            get => _time;
        }

        public long Duration
        {
            get => Time.Duration;
        }

        public int Count
        {
            get => Data.Count;
        }

        public int NrZeros
        {
            get => Time.SamplesAdded;
        }

        public int SamplingFreq
        {
            get => Time.SamplingFreq;
        }

        public int Length
        {
            get => Time.Length;
        }

        public double[] DataAsSignleArray
        {
            get => Data.SelectMany(x => x.Data).ToArray();
        }

        /// <summary>
        /// Adds a signal to the timeseries
        /// </summary>
        /// <param name="inputData"></param>
        public void AddData(IDataPoint inputData)
        {
            Task dataViewTask = inputData.CreateViewer();
            Task timeViewTask = inputData.Time.CreateViewer();
            Task[] tasks = new Task[] { dataViewTask, timeViewTask };
            var dataView = Task.Run(() => inputData.CreateViewer()).Result;
            var timeView = Task.Run(() => inputData.Time.CreateViewer()).Result;
            Task.WaitAll(tasks);
            ITimeSeriesViewer viewer = (ITimeSeriesViewer)dataView;
            viewer.SetTimeRange(timeView.Start, timeView.End);
            var genericConstructor = typeof(DataReader<>).MakeGenericType(inputData.DataType)
            .GetConstructor(new[] { typeof(ITimeSeriesViewer) });
            var dataReader = (IDataReader)genericConstructor.Invoke(new object[] { viewer });
            (long[] timeArray, double[] dataArray, bool[] isNaNArray) = dataReader.DataAsArrays();
            Timeline time = new Timeline(timeArray);
            Signal newSignal = new Signal(dataArray);
            _data.Add(newSignal);
            if (_time == null)
            {
                _time = time;
            }
        }

        /// <summary>
        /// Zero pads the signals in the timeseries
        /// </summary>
        /// <param name="nrZeros"></param>
        public void ZeroPad(int nrZeros)
        {
            double[] zeroArray = new double[nrZeros];
            Array.Clear(zeroArray, 0, nrZeros - 1);
            Time.AddSamples(nrZeros);
            foreach (Signal d in Data)
            {
                d.ZeroPad(zeroArray);
            }
        }

        /// <summary>
        /// Resamples the timeseries,
        /// each signal is resampled individually
        /// </summary>
        /// <param name="newSamplingFreq"></param>
        public void Resample(int newSamplingFreq)
        {
            int nrSamples = (int)(Time.Data.Length * ((newSamplingFreq * 1f) / Time.SamplingFreq));
            double[] oldTimeline = Time.Data.Select(x => Convert.ToDouble(x)).ToArray();
            Time.Resample(nrSamples);
            foreach (Signal d in Data)
            {
                d.Resample(oldTimeline, Time);
            }
        }

        /// <summary>
        /// Subtracts the bias of the timeseries,
        /// bias for each signal is computed induvidually
        /// </summary>
        public void RemoveBias()
        {
            foreach (Signal d in Data)
            {
                d.RemoveBias();
            }
        }

        /// <summary>
        /// Computes the hilbert transform of the timeseries,
        /// each signal is computed induvidually
        /// </summary>
        public void ToHilbertEnvelope()
        {
            foreach (Signal d in Data)
            {
                d.HilbertEnvelope();
            }
        }

    }
    class Signal
    {
        private double[] _data;

        public double[] Data
        {
            get => _data;
        }

        private int _nrZeros = 0;
        public int NrZeros
        {
            get => _nrZeros;
        }

        public int Length
        {
            get => Data.Length;
        }

        public Signal(double[] d)
        {
            _data = d;
        }

        /// <summary>
        /// Adds the zero array to te end of the signal
        /// </summary>
        /// <param name="zeroArray"></param>
        public void ZeroPad(double[] zeroArray)
        {
            _data = Data.Concat(zeroArray).ToArray();
            _nrZeros = zeroArray.Length;
        }

        /// <summary>
        /// Resamples the signal
        /// </summary>
        /// <param name="oldTimeline"></param>
        /// <param name="timeline"></param>
        public void Resample(double[] oldTimeline, Timeline timeline)
        {
            double[] interpolatedData = new double[timeline.Length];
            var interpolationScheme = Interpolate.Linear(oldTimeline, Data);
            for (int i = 0; i < timeline.Length; i++)
            {
                interpolatedData[i] = interpolationScheme.Interpolate(timeline.Data[i]);
            }
            for (int i = 0; i < interpolatedData.Length; i++)
            {
                if (double.IsNaN(interpolatedData[i]))
                {
                    interpolatedData[i] = (interpolatedData[i - 1] + interpolatedData[i + 1]) / 2;
                }
            }

            _data = interpolatedData;
        }

        /// <summary>
        /// Subtracts the bias of the signal
        /// </summary>
        public void RemoveBias()
        {
            double total = 0;
            for (int i = 0; i < Length; i++)
            {
                total += Data[i];
            }

            double mean = total / Length;
            for (int i = 0; i < Length; i++)
            {
                _data[i] -= mean;
            }
        }

        /// <summary>
        /// Computes the hilbert envelope of the signal
        /// </summary>
        public void HilbertEnvelope()
        {
            _data = Hilbert.GetHilbertEnvelope(Data);
        }

    }

    class Timeline
    {
        private long[] _data;
        public long[] Data
        {
            get => _data;
        }

        private int _samplesAdded = 0;
        public int SamplesAdded
        {
            get => _samplesAdded;
        }

        public long Duration
        {
            get => Data[Data.Length - 1] - Data[0];
        }

        public int Length
        {
            get => _data.Length;
        }

        public Timeline(long[] t)
        {
            _data = t;
        }

        public int SamplingFreq
        {
            get
            {
                var denom = Duration / (Length - 1f);
                var samplingFreq = 1000000f / denom;
                int samplingFreqRound = (int)Math.Round(samplingFreq, 0);
                return samplingFreqRound;
            }
        }

        /// <summary>
        /// Adds samples to the timeline
        /// </summary>
        /// <param name="nrSamples"></param>
        public void AddSamples(int nrSamples)
        {
            double samplingTime = (Data[Length - 1] - Data[0]) / (1f * Length);
            long[] newTime = new long[nrSamples];
            for (int i = 0; i < nrSamples; i++)
            {
                newTime[i] = (long)(Data[Length - 1] + (samplingTime * i));
            }
            _data = Data.Concat(newTime).ToArray();
            _samplesAdded = nrSamples;
        }

        /// <summary>
        /// Resamples the timeline, the start and end time will remain the same
        /// </summary>
        /// <param name="nrSamples"></param>
        public void Resample(int nrSamples)
        {
            long[] newTime = new long[nrSamples];

            double startTime = Convert.ToDouble(Data[0]);
            double endTime = Convert.ToDouble(Data[Length - 1]);
            double stepSize = (endTime - startTime) / (nrSamples - 1f);

            for (int i = 0; i < nrSamples; i++)
            {
                double time = (startTime + (i * stepSize));
                newTime[i] = Convert.ToInt64(time);
            }

            _data = newTime;
        }

    }
}
