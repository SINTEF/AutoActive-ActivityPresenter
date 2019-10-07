using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GaitupParser
{
    public class GaitupData
    {

        private readonly List<(long, double, double, double)> _accelerometer;

        public IReadOnlyList<(long, double, double, double)> Accelerometer => _accelerometer;

        private readonly List<(long, double, double, double)> _gyro;
        public IReadOnlyList<(long, double, double, double)> Gyro => _gyro;

        private readonly List<(long, double, double)> _barometer;
        public IReadOnlyList<(long, double, double)> Barometer => _barometer;


        private readonly List<(long, long, double)> _radio = new List<(long, long, double)>();
        public IReadOnlyList<(long, long, double)> Radio => _radio;

        private readonly List<long> _button = new List<long>();
        public IReadOnlyList<long> Button => _button;

        private readonly List<(long, double)> _ble = new List<(long, double)>();
        public IReadOnlyList<(long, double)> Ble => _ble;

        public GaitupConfig Config { get; }

        public long MinTime => Math.Min(Gyro.First().Item1, Accelerometer.First().Item1);

        public long MaxTime => Math.Max(Gyro.Last().Item1, Accelerometer.Last().Item1);

        public GaitupData(GaitupConfig config)
        {
            Config = config;

            _accelerometer = new List<(long, double, double, double)>((int)config.Accelerometer.NumberOfSamples);
            _gyro = new List<(long, double, double, double)>((int)config.Gyro.NumberOfSamples);
            _barometer = new List<(long, double, double)>((int)config.Barometer.NumberOfSamples);
        }

        public void AddAccel((long, double, double, double) data)
        {
            _accelerometer.Add(data);
        }

        public void AddGyro((long, double, double, double) data)
        {
            _gyro.Add(data);
        }

        public void AddBaro((long, double, double) data)
        {
            _barometer.Add(data);
        }

        public void AddRadio((long, long, double) data)
        {
            _radio.Add(data);
        }

        public void AddButton(long data)
        {
            _button.Add(data);
        }

        public void AddBle((long, double) data)
        {
            _ble.Add(data);
        }

        public void Write(string path)
        {
            using (var file = File.CreateText(path))
            {
                file.WriteLine("time, acc_x, acc_y, acc_z, gyr_x, gyr_y, gyr_z, bar_pressure, bar_temp, btn");

                var gyrIt = Gyro.GetEnumerator();
                var barIt = Barometer.GetEnumerator();
                var btnIt = Button.GetEnumerator();

                gyrIt.MoveNext();
                barIt.MoveNext();
                btnIt.MoveNext();

                foreach (var acc in Accelerometer)
                {
                    var (t, accX, accY, accZ) = acc;
                    var (gyrT, gyrX, gyrY, gyrZ) = gyrIt.Current;
                    var (barT, barPressure, barTemp) = barIt.Current;

                    file.Write($"{t / (double)Config.Frequency},");
                    file.Write($"{accX},{accY},{accZ},");

                    if (gyrT == t)
                    {
                        file.Write($"{gyrX},{gyrY},{gyrZ},");
                        gyrIt.MoveNext();
                    }
                    else
                    {
                        file.Write(",,,");
                    }

                    if (barT == t)
                    {
                        file.Write($"{barPressure},{barTemp},");
                        barIt.MoveNext();
                    }
                    else
                    {
                        file.Write(",,");
                    }

                    if (btnIt.Current == t)
                    {
                        file.WriteLine(btnIt.Current);
                        btnIt.MoveNext();
                    }
                    else
                    {
                        file.WriteLine("");
                    }
                    
                }
                gyrIt.Dispose();
                barIt.Dispose();
                btnIt.Dispose();
            }
        }

        public void OffsetTime(long offset)
        {
            for (var i = 0; i < _accelerometer.Count; i++)
            {
                var point = _accelerometer[i];
                point.Item1 += offset;
                _accelerometer[i] = point;
            }

            for (var i = 0; i < _gyro.Count; i++)
            {
                var point = _gyro[i];
                point.Item1 += offset;
                _gyro[i] = point;
            }

            for (var i = 0; i < _barometer.Count; i++)
            {
                var point = _barometer[i];
                point.Item1 += offset;
                _barometer[i] = point;
            }

            for (var i = 0; i < _button.Count; i++)
            {
                _button[i] += offset;
            }
        }

        private static void CropList<T>(long startTime, long endTime, List<T> listToCrop, Func<T, long> time)
        {
            var endIndex = listToCrop.FindIndex(p => time(p) > endTime);
            var startIndex = listToCrop.FindLastIndex(p => time(p) < startTime);
            if (endIndex > 0) listToCrop.RemoveRange(endIndex, listToCrop.Count - endIndex);
            if (startIndex > 0) listToCrop.RemoveRange(0, startIndex + 1);
            listToCrop.TrimExcess();
        }

        public void Crop(long startTime, long endTime)
        {
            CropList(startTime, endTime, _accelerometer, (p => p.Item1));
            CropList(startTime, endTime, _gyro, (p => p.Item1));
            CropList(startTime, endTime, _barometer, (p => p.Item1));
            CropList(startTime, endTime, _button, (p => p));
        }
    }
}