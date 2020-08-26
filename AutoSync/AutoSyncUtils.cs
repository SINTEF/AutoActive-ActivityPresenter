using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SINTEF.AutoActive.AutoSync
{
    class AutoSyncUtils
    {
        public static class Hilbert
        {
            public static Complex32[] HilbertTransform(double[] signal)
            {
                int n = signal.Length;
                int halfLength = (int)Math.Floor(n / 2f);

                Complex32[] complexSignal = FFT.ToComplex32Array(signal);
                complexSignal = FFT.fft(complexSignal);

                for (int i = 0; i < halfLength; i++)
                {
                    complexSignal[i] *= 2;
                }

                for (int i = halfLength; i < n; i++)
                {
                    complexSignal[i] = 0;
                }

                complexSignal = FFT.ifft(complexSignal);
                return complexSignal;

            }

            public static double[] GetHilbertEnvelope(double[] signal)
            {
                Complex32[] complexSignal = HilbertTransform(signal);
                double[] envelope = new double[complexSignal.Length];
                for (int i = 0; i < complexSignal.Length; i++)
                {
                    envelope[i] = complexSignal[i].Magnitude;
                }
                return envelope;
            }
        }

        public static class FFT
        {
            public static Complex32[] ToComplex32Array(double[] signal)
            {
                return signal.Select((num, index) => new Complex32((float)num, 0)).ToArray();
            }

            public static Complex32[] fft(Complex32[] signal)
            {
                Fourier.Forward(signal, FourierOptions.Matlab);
                return signal;
            }

            public static Complex32[] ifft(Complex32[] signal)
            {
                Fourier.Inverse(signal, FourierOptions.Matlab);
                return signal;
            }
        }


    }
}
