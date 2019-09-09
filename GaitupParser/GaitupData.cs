using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace GaitupParser
{
    public class GaitupData
    {

        private List<(long, double, double, double)> _accelerometer;

        public IReadOnlyList<(long, double, double, double)> Accelerometer => _accelerometer;

        private List<(long, double, double, double)> _gyro;
        public IReadOnlyList<(long, double, double, double)> Gyro => _gyro;

        private List<(long, double, double)> _barometer;
        public IReadOnlyList<(long, double, double)> Barometer => _barometer;


        private readonly List<(long, long, double)> _radio = new List<(long, long, double)>();
        public IReadOnlyList<(long, long, double)> Radio => _radio;

        private List<long> _button = new List<long>();
        public IReadOnlyList<long> Button => _button;

        private readonly List<(long, double)> _ble = new List<(long, double)>();
        public IReadOnlyList<(long, double)> Ble => _ble;

        public GaitupConfig Config { get; }

        private readonly SortedSet<long> _timestamps;
        public IReadOnlyCollection<long> Timestamps => _timestamps;
        public GaitupData(GaitupConfig config)
        {
            Config = config;

            _accelerometer = new List<(long, double, double, double)>((int)config.Accelerometer.NumberOfSamples);
            _gyro = new List<(long, double, double, double)>((int)config.Gyro.NumberOfSamples);
            _barometer = new List<(long, double, double)>((int)config.Barometer.NumberOfSamples);
            _timestamps = new SortedSet<long>();
        }

        private void AddTimestamp(long time)
        {
            _timestamps.Add(time);
        }

        public void AddAccel((long, double, double, double) data)
        {
            AddTimestamp(data.Item1);
            _accelerometer.Add(data);
        }

        public void AddGyro((long, double, double, double) data)
        {
            AddTimestamp(data.Item1);
            _gyro.Add(data);
        }

        public void AddBaro((long, double, double) data)
        {
            AddTimestamp(data.Item1);
            _barometer.Add(data);
        }

        public void AddRadio((long, long, double) data)
        {
            AddTimestamp(data.Item1);
            _radio.Add(data);
        }

        public void AddButton(long data)
        {
            AddTimestamp(data);
            _button.Add(data);
        }

        public void AddBle((long, double) data)
        {
            AddTimestamp(data.Item1);
            _ble.Add(data);
        }

        public void Write(string path)
        {
            using (var file = File.CreateText(path))
            {
                file.WriteLine("time, acc_x, acc_y, acc_z, gyr_x, gyr_y, gyr_z, bar_pressure, bar_temp, btn");

                var accIt = Accelerometer.GetEnumerator();
                var gyrIt = Gyro.GetEnumerator();
                var barIt = Barometer.GetEnumerator();
                var btnIt = Button.GetEnumerator();

                accIt.MoveNext();
                gyrIt.MoveNext();
                barIt.MoveNext();
                btnIt.MoveNext();

                foreach (var t in Timestamps)
                {
                    var (accT, accX, accY, accZ) = accIt.Current;
                    var (gyrT, gyrX, gyrY, gyrZ) = gyrIt.Current;
                    var (barT, barPressure, barTemp) = barIt.Current;

                    file.Write($"{t / (double)Config.Frequency},");

                    if (gyrT == t)
                    {
                        file.Write($"{gyrX},{gyrY},{gyrZ},");
                        gyrIt.MoveNext();
                    }
                    else
                    {
                        file.Write(",,,");
                    }

                    if (accT == t)
                    {
                        file.Write($"{accX},{accY},{accZ},");
                        accIt.MoveNext();
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
                accIt.Dispose();
                gyrIt.Dispose();
                barIt.Dispose();
                btnIt.Dispose();
            }
        }

        public void OffsetTime(long offset)
        {
            _timestamps.Clear();

            for (var i = 0; i < _accelerometer.Count; i++)
            {
                var point = _accelerometer[i];
                point.Item1 += offset;
                _accelerometer[i] = point;
                _timestamps.Add(point.Item1);
            }

            for (var i = 0; i < _gyro.Count; i++)
            {
                var point = _gyro[i];
                point.Item1 += offset;
                _gyro[i] = point;
                _timestamps.Add(point.Item1);
            }

            for (var i = 0; i < _barometer.Count; i++)
            {
                var point = _barometer[i];
                point.Item1 += offset;
                _barometer[i] = point;
                _timestamps.Add(point.Item1);
            }

            for (var i = 0; i < _button.Count; i++)
            {
                _button[i] += offset;
                _timestamps.Add(_button[i]);
            }
        }

        public void Crop(int startTime, long endTime)
        {

            _accelerometer = _accelerometer.FindAll(p => p.Item1 >= startTime && p.Item1 <= endTime);
            _gyro = _gyro.FindAll(p => p.Item1 >= startTime && p.Item1 <= endTime);
            _barometer = _barometer.FindAll(p => p.Item1 >= startTime && p.Item1 <= endTime);
            _button = _button.FindAll(p => p >= startTime && p <= endTime);

            _timestamps.Clear();
            foreach (var point in _accelerometer)
            {
                _timestamps.Add(point.Item1);
            }
        }
    }
}