using System;
using SINTEF.AutoActive.Databus.Interfaces;
using static SINTEF.AutoActive.AutoSync.AutoSyncUtils;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra.Complex32;
using Accord.Math;
using System.Threading.Tasks;



namespace SINTEF.AutoActive.AutoSync
{
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

        private List<IDataPoint> _masterTimerseries = new List<IDataPoint>();

        public void AddMasterSignal(IDataPoint signal)
        {
            _masterTimerseries.Add(signal);
        }

        private List<IDataPoint> _slaveTimerseries = new List<IDataPoint>();

        public void AddSlaveSignal(IDataPoint signal)
        {
            _slaveTimerseries.Add(signal);
        }

        /// <summary>
        /// Correlates the master and slave timeseries
        /// </summary>
        /// <returns>A tuple containing the lag and correlation</returns>
        public (long[], float[], string errorMessage) CorrelateSignals()
        {
            string errorMessage = null;
            if ((_masterTimerseries.Count) != (_slaveTimerseries.Count))
            {
                errorMessage = "There must be equally many signals in the slave and master timeseries.";
                return (null, null, errorMessage);
            }
            if (_masterTimerseries.Count == 0)
            {
                errorMessage = "The master timeseries must at least consist of one signal";
                return (null, null, errorMessage);
            }

            Timeserie masterTimerserie = new Timeserie();
            Timeserie slaveTimerserie = new Timeserie();

            for (int i = _masterTimerseries.Count - 1; i > -1; i--)
            {
                masterTimerserie.AddData(_masterTimerseries[i]);
                slaveTimerserie.AddData(_slaveTimerseries[i]);
            }

            if (masterTimerserie.Duration < slaveTimerserie.Duration)
            {
                errorMessage = "The signals in the master timeserie must be equally long or longer then the " +
                    "signals in the slave timeserie";
                return (null, null, errorMessage);
            }

            ResampleTimeserie(masterTimerserie, slaveTimerserie);
            masterTimerserie.RemoveBias();
            slaveTimerserie.RemoveBias();
            masterTimerserie.ToHilbertEnvelope();
            slaveTimerserie.ToHilbertEnvelope();
            AdjustLengthOfTimeserie(masterTimerserie, slaveTimerserie);

            var cor = CrossCorrelation(masterTimerserie.DataAsSignleArray, slaveTimerserie.DataAsSignleArray);
            int zeroIndex = (int)Math.Ceiling(((masterTimerserie.Length * masterTimerserie.Count * 2f) - 1) / 2);
            int fromIndex = zeroIndex - masterTimerserie.Length + slaveTimerserie.NrZeros;
            int nrInterestingSamples = (masterTimerserie.Length + slaveTimerserie.Length - slaveTimerserie.NrZeros) - 1;
            cor = cor.Skip(fromIndex).Take(nrInterestingSamples).ToArray();
            var timelag = IndexToTime(masterTimerserie, slaveTimerserie, cor.Length);

            return (timelag, cor, errorMessage);
        }

        private long[] IndexToTime(Timeserie master, Timeserie slave, int nrSamples)
        {
            long durationSlave = slave.Time.Data[slave.Length - 1 - slave.NrZeros] - slave.Time.Data[0];
            long durationMaster = master.Time.Data[master.Length - 1] - master.Time.Data[0];
            long shiftedFromZero = slave.Time.Data[0] - master.Time.Data[0];

            long startTime = shiftedFromZero + durationSlave;
            long endTime = startTime - durationMaster - durationSlave;
            long duration = Math.Abs(startTime - endTime);

            float sampleTime = duration / (nrSamples - 1);
            long[] timeShift = new long[nrSamples];

            for (int i = 0; i < nrSamples; i++)
            {
                timeShift[i] = (long)-(startTime - (i * sampleTime));
            }

            return timeShift;

        }

        private float[] CrossCorrelation(double[] x1, double[] x2)
        {
            int length = x1.Length + x2.Length - 1;
            Complex32[] zeroArray = DenseVector.Create(length - x1.Length, 0).ToArray();
            Array.Reverse(x2);
            Func<double[], Complex32[]> convertToComplex = x => x.Select((num, index) => new Complex32((float)num, 0)).ToArray();

            Complex32[] comX1 = FFT.ToComplex32Array(x1);
            Complex32[] comX2 = FFT.ToComplex32Array(x2);
            Complex32[] zeroPaddedComX1 = comX1.Concat(zeroArray).ToArray();
            Complex32[] zeroPaddedcomX2 = comX2.Concat(zeroArray).ToArray();

            zeroPaddedComX1 = FFT.fft(zeroPaddedComX1);
            zeroPaddedcomX2 = FFT.fft(zeroPaddedcomX2);
            var results = zeroPaddedComX1.Zip(zeroPaddedcomX2, (a, b) => (a * b)).ToArray();

            results = FFT.ifft(results);
            return results.Select(x => x.Real).ToArray();

        }

        /// <summary>
        /// Resamples the timeseries. The timeseries is resampled to the highest frequency of either the master or slave
        /// </summary>
        /// <param name="masterTimerseries"></param>
        /// <param name="slaveTimerseries"></param>
        private void ResampleTimeserie(Timeserie masterTimerserie, Timeserie slaveTimerserie)
        {
            if (masterTimerserie.SamplingFreq == slaveTimerserie.SamplingFreq)
            {
                return;
            }
            else if (masterTimerserie.SamplingFreq > slaveTimerserie.SamplingFreq)
            {
                slaveTimerserie.Resample(masterTimerserie.SamplingFreq);
            }
            else
            {
                masterTimerserie.Resample(slaveTimerserie.SamplingFreq);
            }
        }

        /// <summary>
        /// If the master timeseries have a longer duration compared to the slave timeseries and we use multiple timeseries to
        /// sync the timeseries, we need to zero pad the slaves, to not get a offset
        /// </summary>
        /// <param name="masterTimerseries"></param>
        /// <param name="slaveTimerseries"></param>
        private void AdjustLengthOfTimeserie(Timeserie masterTimerserie, Timeserie slaveTimerserie)
        {
            if (masterTimerserie.Length > slaveTimerserie.Length)
            {
                int nrZeros = masterTimerserie.Length - slaveTimerserie.Length;
                slaveTimerserie.ZeroPad(nrZeros);
            }
        }

    }
}