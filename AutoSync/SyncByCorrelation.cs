using System;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Common;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;
using MathNet.Numerics.LinearAlgebra.Complex32;
using System.IO;
//using MathNet.Numerics.Interpolation;


namespace SINTEF.AutoActive.AutoSync
{
    /// <summary>
    /// Dataclass
    /// </summary>
    /// <remarks>
    /// Intended to be used as a helper class for SyncByCorrelation class
    /// </remarks>
    internal class timeseries
    {
        private List<double[]> _data = new List<double[]>();
        private long[] _time;
        private int _nrZeros = 0;

        /// <summary>
        /// Returns the number of signals the simeseries consist of
        /// </summary>
        public int Count
        {
            get => _data.Count;
        }

        /// <summary>
        /// Returns the length of a signal
        /// </summary>
        public int Length
        {
            get => _data[0].Length;
        }

        /// <summary>
        /// Returns the timeline 
        /// </summary>
        public long[] Time
        {
            get => _time;
        }

        /// <summary>
        /// Returns the signals
        /// </summary>
        public List<double[]> Data
        {
            get => _data;
        }

        /// <summary>
        /// Returns multiple signals as a single array
        /// </summary>
        public double[] DataAsSignleArray
        {
            get => _data.SelectMany(x => x).ToArray();
        }

        /// <summary>
        /// Returns the samplingfrequency of the timeseries
        /// </summary>
        public int SamplingFreq 
        {
            get => (int)Math.Round((1000000f / (_time[1] - _time[0])),0);
        }

        public double Duration
        {
            get => _time[_time.Length - 1] - _time[0];
        }

        public int NrZeros
        {
            get => _nrZeros;
        }

        /// <summary>
        /// Function for adding signals to the timeseries
        /// </summary>
        /// <param name="inputData"></param>
        public void AddData(IDataPoint inputData)
        {
            Task dataViewTask = inputData.CreateViewer();
            Task timeViewTask = inputData.Time.CreateViewer();
            Task[] tasks = new Task[]{ dataViewTask, timeViewTask };
            var dataView = Task.Run(() => inputData.CreateViewer()).Result;
            var timeView = Task.Run(() => inputData.Time.CreateViewer()).Result;
            Task.WaitAll(tasks);
            ITimeSeriesViewer viewer = (ITimeSeriesViewer)dataView;
            viewer.SetTimeRange(timeView.Start, timeView.End);
            var span = viewer.GetCurrentData<double>();
            long[] time = span.X.ToArray();
            double[] data = span.Y.ToArray();
            _data.Add(data);
            if (_time == null);
            {
                _time = time.Cast<long>().ToArray(); ;
            }

        }

        /// <summary>
        /// Adds zeros to the end of each signal in the timeseries 
        /// </summary>
        /// <param name="nrZeros">Number of zeros to add</param>
        internal void zeroPadSignals(int nrZeros)
        {
            double[] zeroArray = new double[nrZeros];
            zeroArray = zeroArray.Select(x => x = 0).ToArray();
            _data = _data.Select(x => x.Concat(zeroArray).ToArray()).ToList();
            
            long timediff = Time[1] - Time[0];
            long endtime = Time[Time.Length - 1];
            long[] zeroPaddedTime = new long[zeroArray.Length];
            for (int i = 0; i < zeroArray.Length; i++)
            {
                zeroPaddedTime[i] = endtime + ((i + 1) * timediff);
            }

            _time = _time.Concat(zeroPaddedTime).ToArray();
            _nrZeros = zeroArray.Length;
        }

        /// <summary>
        /// Resamples the signals of the timeseries
        /// </summary>
        /// <param name="newSamplingFreq">The new sampling frequency</param>
        internal void ResampleSignals(int newSamplingFreq)
        {
            int arraySize = Length * (newSamplingFreq/SamplingFreq);
            long[] newTimeline = ComputeNewTimeline(arraySize); 
            _data = _data.Select(x => InterpolateSignal(x, Time, newTimeline)).ToList();
            _time = newTimeline;
        }

        /// <summary>
        /// Computes the new timeline for the resampled signal
        /// </summary>
        /// <param name="arraySize"> The size of the new timeline</param>
        /// <returns>The new timeline</returns>
        private long[] ComputeNewTimeline(int arraySize)
        {
            long[] newTimeline = new long[arraySize];

            var startTime = _time[0];
            var endTime = _time[Length - 1];
            var stepSize = (endTime - startTime) / (arraySize-2);

            for (int i = 0; i < arraySize; i++)
            {
                newTimeline[i] = startTime + (i * stepSize);
            }

            return newTimeline;
        }

        private double[] InterpolateSignal(double[] signal, long[] timeline, long[] newTimeline)
        {
            double[] doubleTimeline = timeline.Select((x,i) => (double)x).ToArray();
            var interpolationScheme = Interpolate.Linear(doubleTimeline, signal);
            double[] interpolatedSignal = newTimeline.Select((x, i) => interpolationScheme.Interpolate(x)).ToArray();
            return interpolatedSignal;
        }

    }


    /// <summary>
    /// Synchronises timeseries by correlation
    /// </summary>
    /// <remarks>
    /// A timeseries can possible consist of multiple signals
    /// When using multiple signals in one tomeseries the timeline of the signals must be exactly the same.
    /// The masterTimerseries must have a long duration compared to the slaveTimerseries.
    /// The number of signals added to  _masterTimerseries must be the same as the number added to  _slaveTimerseries
    /// </remarks>>
    public class SyncByCorrelation
    {

        private List<IDataPoint>  _masterTimerseries = new List<IDataPoint>();

        public void AddMasterSignal(IDataPoint signal)
        {

            _masterTimerseries.Add(signal);

        }

        private List<IDataPoint>  _slaveTimerseries = new List<IDataPoint> ();

        public void AddSlaveSignal(IDataPoint signal)
        {

            _slaveTimerseries.Add(signal);

        }

        public SyncByCorrelation()
        {


        }

#if DEBUG
        ~SyncByCorrelation()
        {
            Console.WriteLine("The object was destructed");
        }
#endif

        /// <summary>
        /// Correlates the master and slave timeseries
        /// </summary>
        /// <returns>A tuple containing the lag and correlation</returns>
        public (long[], float[]) CorrelateSignals()
        {
            if (( _masterTimerseries.Count) != ( _slaveTimerseries.Count))
            {
                throw new Exception("");
            }
            if ( _masterTimerseries.Count == 0)
            {
                throw new Exception("");
            }

            timeseries masterTimerseries = new timeseries();
            timeseries slaveTimerseries = new timeseries();

            for (int i = _masterTimerseries.Count - 1; i >  -1; i--)
            {
                masterTimerseries.AddData( _masterTimerseries[i]);
                slaveTimerseries.AddData( _slaveTimerseries[i]);
            }

            //if (slaveTimerseries.Duration > masterTimerseries.Duration)
            //{
            //    throw new Exception("The length of the slave datapoints must be shorter then the master");
            //}

            ResampleSignals(masterTimerseries, slaveTimerseries);
            //if ((slaveTimerseries.Duration < masterTimerseries.Duration) & (masterTimerseries.Count > 1))
            //{
            AdjustLengthOfSignals(masterTimerseries, slaveTimerseries);
            //}
            var cor = CrossCorrelation(masterTimerseries.DataAsSignleArray, slaveTimerseries.DataAsSignleArray);
            int zeroIndex =  (int)Math.Ceiling(((masterTimerseries.Length * masterTimerseries.Count * 2f) - 1) / 2);
            int fromIndex = zeroIndex - masterTimerseries.Length + slaveTimerseries.NrZeros;
            int nrInterestingSamples = (masterTimerseries.Length + slaveTimerseries.Length - slaveTimerseries.NrZeros) - 1;
            string[] cor_string = cor.Select((x, i) => x.ToString()).ToArray();
            cor = cor.Skip(fromIndex).Take(nrInterestingSamples).ToArray();
            var timelag = cor.Select((num, index) => IndexToTimeShift(masterTimerseries, slaveTimerseries, 0, index)).ToArray();
            return (timelag, cor);
        }

        /// <summary>
        /// Converts Index to timeshift
        /// </summary>
        /// <param name="lag">The lag to be converted</param>
        /// <returns>The timeshift</returns>
        private long IndexToTimeShift(timeseries master, timeseries slave, int nrZeros, int index)
        {
            var shiftedFromZero = slave.Time[0] - master.Time[master.Length - 1];
            long timeResolution = master.Time[1] - master.Time[0];
            long timeShift = shiftedFromZero + (timeResolution * index);

            return timeShift;
        }

        private float[] CrossCorrelation(double[] x1, double[] x2)
        {
            int length = x1.Length + x2.Length - 1;
            Complex32[] zeroArray = DenseVector.Create(length - x1.Length, 0).ToArray();
            Array.Reverse(x2);
            Func<double[], Complex32[]> convertToComplex = x => x.Select((num, index) => new Complex32((float)num, 0)).ToArray();


            Complex32[] comX1 = convertToComplex(x1);
            Complex32[] comX2 = convertToComplex(x2);
            Complex32[] zeroPaddedComX1 = comX1.Concat(zeroArray).ToArray();
            Complex32[] zeroPaddedcomX2 = comX2.Concat(zeroArray).ToArray();
            
            Fourier.Forward(zeroPaddedComX1, FourierOptions.Matlab);
            Fourier.Forward(zeroPaddedcomX2, FourierOptions.Matlab);
            var results = zeroPaddedComX1.Zip(zeroPaddedcomX2, (a, b) => (a * b)).ToArray();
            
            Fourier.Inverse(results, FourierOptions.Matlab);
            return results.Select(x => x.Real).ToArray();
             
        }

        /// <summary>
        /// Resamples the timeseries. The timeseries is resampled to the highest frequency of either the master or slave
        /// </summary>
        /// <param name="masterTimerseries"></param>
        /// <param name="slaveTimerseries"></param>
        private void ResampleSignals(timeseries masterTimerseries, timeseries slaveTimerseries)
        {
            if (masterTimerseries.SamplingFreq == slaveTimerseries.SamplingFreq)
            {
                return;
            }
            else if (masterTimerseries.SamplingFreq > slaveTimerseries.SamplingFreq)
            {
                slaveTimerseries.ResampleSignals(masterTimerseries.SamplingFreq);
            }
            else
            {
                masterTimerseries.ResampleSignals(slaveTimerseries.SamplingFreq);
            }
        }

        /// <summary>
        /// If the master timeseries have a longer duration compared to the slave timeseries and we use multiple timeseries to 
        /// sync the timeseries, we need to zero pad the slaves, to not get a offset
        /// </summary>
        /// <param name="masterTimerseries"></param>
        /// <param name="slaveTimerseries"></param>
        private void AdjustLengthOfSignals(timeseries masterTimerseries, timeseries slaveTimerseries)
        {
            if (masterTimerseries.Length > slaveTimerseries.Length)
            {
                int nrZeros = masterTimerseries.Length - slaveTimerseries.Length;
                slaveTimerseries.zeroPadSignals(nrZeros);
            }
        }

    }
}
