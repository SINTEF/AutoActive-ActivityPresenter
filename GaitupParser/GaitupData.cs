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

        public long MinTime { get; private set; }
        public long MaxTime { get; private set; }
        private bool _firstTime;
        public GaitupData(GaitupConfig config)
        {
            Config = config;

            _accelerometer = new List<(long, double, double, double)>((int)config.Accelerometer.NumberOfSamples);
            _gyro = new List<(long, double, double, double)>((int)config.Gyro.NumberOfSamples);
            _barometer = new List<(long, double, double)>((int)config.Barometer.NumberOfSamples);
            _firstTime = true;
        }

        private void AddTimestamp(long time)
        {
            if(_firstTime)
            {
                _firstTime = false;
                MaxTime = time;
                MinTime = time;
            }
            else
            {
                if (MaxTime < time) MaxTime = time;
                if (MinTime > time) MinTime = time;
            }
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

#if false
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
#endif

        public void OffsetTime(long offset)
        {
            _firstTime = true;

            for (var i = 0; i < _accelerometer.Count; i++)
            {
                var point = _accelerometer[i];
                point.Item1 += offset;
                _accelerometer[i] = point;
                AddTimestamp(point.Item1);
            }

            for (var i = 0; i < _gyro.Count; i++)
            {
                var point = _gyro[i];
                point.Item1 += offset;
                _gyro[i] = point;
                AddTimestamp(point.Item1);
            }

            for (var i = 0; i < _barometer.Count; i++)
            {
                var point = _barometer[i];
                point.Item1 += offset;
                _barometer[i] = point;
                AddTimestamp(point.Item1);
            }

            for (var i = 0; i < _button.Count; i++)
            {
                _button[i] += offset;
                AddTimestamp(_button[i]);
            }
        }

        private void CropList<T>(int startTime, long endTime, List<T> listToCrop, Func<T, long> time)
        {
            var endIndex = listToCrop.FindIndex(p => time(p) > endTime);
            var startIndex = listToCrop.FindLastIndex(p => time(p) < startTime);
            if (endIndex > 0) listToCrop.RemoveRange(endIndex, listToCrop.Count - 1 - endIndex);
            if (startIndex > 0) listToCrop.RemoveRange(0, startIndex + 1);
            listToCrop.TrimExcess();
        }

        public void Crop(int startTime, long endTime)
        {
            //var memoryStart = System.GC.GetTotalMemory(true);
            CropList(startTime, endTime, _accelerometer, (p => p.Item1));
            CropList(startTime, endTime, _gyro, (p => p.Item1));
            CropList(startTime, endTime, _barometer, (p => p.Item1));
            CropList(startTime, endTime, _button, (p => p));
            //var memoryEnd = System.GC.GetTotalMemory(true);

            _firstTime = true;
            foreach (var point in _accelerometer)
            {
                AddTimestamp(point.Item1);
            }
        }
    }
}