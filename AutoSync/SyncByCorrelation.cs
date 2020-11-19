using System;
using SINTEF.AutoActive.Databus.Interfaces;
using static SINTEF.AutoActive.AutoSync.AutoSyncUtils;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra.Complex32;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;

namespace SINTEF.AutoActive.AutoSync
{
    /// <summary>
    /// Synchronises timeseries by correlation
    /// </summary>
    /// <remarks>
    /// A timeseries can consist of multiple signals
    /// When using multiple signals in one timeserie the timeline of the signals must be exactly the same.
    /// The number of signals added to  _masterTimerserie must be the same as the number added to _slaveTimerserie
    /// </remarks>>
    public class SyncByCorrelation
    {

        private List<IDataPoint> _masterTimerserie = new List<IDataPoint>();

        public void AddMasterSignal(IDataPoint signal)
        {
            _masterTimerserie.Add(signal);
        }

        private List<IDataPoint> _slaveTimerserie = new List<IDataPoint>();

        public void AddSlaveSignal(IDataPoint signal)
        {
            _slaveTimerserie.Add(signal);
        }

        /// <summary>
        /// Correlates the master and slave timeseries
        /// </summary>
        /// <returns>A tuple containing the lag , correlation and any error messages</returns>
        public (long[], float[], string errorMessage) CorrelateSignals()
        {
            string errorMessage = null;
            if ((_masterTimerserie.Count) != (_slaveTimerserie.Count))
            {
                errorMessage = "There must be equally many signals in the slave and master timeseries.";
                return (null, null, errorMessage);
            }
            if (_masterTimerserie.Count == 0)
            {
                errorMessage = "The master timeseries must at least consist of one signal";
                return (null, null, errorMessage);
            }

            Timeserie masterTimerserie = new Timeserie();
            Timeserie slaveTimerserie = new Timeserie();

            for (int i = _masterTimerserie.Count - 1; i > -1; i--)
            {
                if (_masterTimerserie[i] is TableColumn)
                {
                    masterTimerserie.AddData(_masterTimerserie[i]);
                }
                else
                {
                    errorMessage = "The data must be a 1d timeseries";
                    return (null, null, errorMessage);
                }
                if (_slaveTimerserie[i] is TableColumn)
                {
                    slaveTimerserie.AddData(_slaveTimerserie[i]);
                }
                else
                {
                    errorMessage = "The data must be a 1d timeseries";
                    return (null, null, errorMessage);
                }
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
            int nrInterestingSamples = (masterTimerserie.Length + slaveTimerserie.Length - slaveTimerserie.NrZeros - masterTimerserie.NrZeros) - 1;
            cor = cor.Skip(fromIndex).Take(nrInterestingSamples).ToArray();
            var timelag = IndexToTime(masterTimerserie, slaveTimerserie, cor.Length);

            return (timelag, cor, errorMessage);
        }

        /// <summary>
        /// maps the index nr to a time lag
        /// </summary>
        /// <param name="master"></param>
        /// <param name="slave"></param>
        /// <param name="nrSamples"></param>
        /// <returns> A array mapping index to a time lag </returns>
        private long[] IndexToTime(Timeserie master, Timeserie slave, int nrSamples)
        {
            long durationSlave = slave.Time.Data[slave.Length - 1 - slave.NrZeros] - slave.Time.Data[0];
            long durationMaster = master.Time.Data[master.Length - 1 - master.NrZeros] - master.Time.Data[0];
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

        /// <summary>
        /// Computes the cross correlation between the two signals
        /// </summary>
        /// <param name="x1"></param>
        /// <param name="x2"></param>
        /// <returns>The cross correlation</returns>
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
        /// Resamples the timeseries. The timeserie of the lowest sampling frequency is resampled to
        /// the same sampling frequency as the other timeserie
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
        /// If the master timeseries have a longer duration compared to the slave timeseries and we use multiple signals to
        /// sync the timeseries, we need to zero pad the slaves.
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
            if (slaveTimerserie.Length > masterTimerserie.Length)
            {
                int nrZeros = slaveTimerserie.Length - masterTimerserie.Length;
                masterTimerserie.ZeroPad(nrZeros);
            }
        }

    }
}