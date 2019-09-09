using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GaitupTest")]
namespace GaitupParser
{
    public class GaitupParser
    {
        private readonly Stream _stream;
        private readonly BinaryReader _reader;
        private GaitupData _data;
        public bool PrintProgress { get; set; }

        public GaitupParser(Stream stream)
        {
            _stream = stream;
            _reader = new BigEndianBinaryReader(_stream);
        }

        internal GaitupConfigFrame GetConfigFrame(GaitupConfig config)
        {
            return GaitupConfigFrame.NextFrame(config, _reader);
        }

        public GaitupConfig GetConfig()
        {
            var config = new GaitupConfig();
            while(true)
            {
                var frame = GetConfigFrame(config);
                if(frame == null)
                {
                    break;
                }
            }
            return config;
        }
        private readonly byte[] _buffer = new byte[SectorLength];
        private void ParseSector(GaitupConfig config)
        {
            if (_stream.Read(_buffer, 0, SectorLength) == 0)
            {
                throw new EndOfStreamException();
            }

            var sectorId = BigEndianBinaryReader.GetUInt32(_buffer, 0);
            var sectorTimestamp = BigEndianBinaryReader.GetUInt32(_buffer, 4);

            for (var i = 8; i < SectorLength; i += DataPayload)
            {
                var sensorId = _buffer[i];
                var subTimestamp = _buffer[i + 1];
                long timestamp = sectorTimestamp + subTimestamp;
                if (config.Accelerometer.Id == sensorId)
                {
                    var x = (BigEndianBinaryReader.GetInt16(_buffer, i + 2) * config.Accelerometer.Scale / 32768.0 - config.Accelerometer.OffsetX) / config.Accelerometer.GainX;
                    var y = (BigEndianBinaryReader.GetInt16(_buffer, i + 4) * config.Accelerometer.Scale / 32768.0 - config.Accelerometer.OffsetY) / config.Accelerometer.GainY;
                    var z = (BigEndianBinaryReader.GetInt16(_buffer, i + 6) * config.Accelerometer.Scale / 32768.0 - config.Accelerometer.OffsetZ) / config.Accelerometer.GainZ;
                    _data.AddAccel((timestamp, x, y, z));
                }
                else if (config.Gyro.Id == sensorId)
                {
                    var x = (BigEndianBinaryReader.GetInt16(_buffer, i + 2) * config.Gyro.Scale / 32768.0 - config.Gyro.OffsetX) / config.Gyro.GainX;
                    var y = (BigEndianBinaryReader.GetInt16(_buffer, i + 4) * config.Gyro.Scale / 32768.0 - config.Gyro.OffsetY) / config.Gyro.GainY;
                    var z = (BigEndianBinaryReader.GetInt16(_buffer, i + 6) * config.Gyro.Scale / 32768.0 - config.Gyro.OffsetZ) / config.Gyro.GainZ;
                    _data.AddGyro((timestamp, x, y, z));
                }
                else if (config.Barometer.Id == sensorId)
                {
                    var tmp = new byte[4];
                    for(var j=0; j<3; j++)
                    {
                        tmp[j] = _buffer[i + 2 + j];
                    }
                    var pressure = BitConverter.ToInt32(tmp, 0) / 4096.0;
                    var temperature = BitConverter.ToInt16(_buffer, i + 5) / 100.0;
                    _data.AddBaro((timestamp, pressure, temperature));
                }
                else if (config.Button.Id == sensorId)
                {
                    var press = _buffer[i + 2];
                    if (press != 1)
                    {
                        Debug.WriteLine($"None-one button-press: {press}");
                    }
                    _data.AddButton(timestamp);
                } else if (config.Radio.Id == sensorId)
                {
                    var timestamp2 = BigEndianBinaryReader.GetInt32(_buffer, i + 2);
                    var val = (double) timestamp2 / config.Frequency;
                    _data.AddRadio((timestamp, timestamp2, val));
                }
                else if(config.Ble.Id == sensorId)
                {
                    throw new NotImplementedException("BLE sync not implemented yet");
                    // This looks really weird, as i+7 could be outside the buffer.
                    var val = BitConverter.ToDouble(_buffer, i + 7);
                    _data.AddBle((timestamp, val));
                }
                else
                {

                }
            }
        }

        private const int SectorLength = 0x200;
        private const int DataPayload = 8;

        public void ParseFile(GaitupConfig config = null)
        {
            if (config == null)
            {
                config = GetConfig();
            }

            var fileLength = _stream.Length;

            // Ensure we are at the start of the first sector
            _reader.BaseStream.Seek(SectorLength, SeekOrigin.Begin);

            _data = new GaitupData(config);
            var lastPrint = -1;
            try
            {
                while (true)
                {
                    ParseSector(config);

                    if (!PrintProgress) continue;

                    var print = (int)(_stream.Position * 100 / fileLength);
                    if (print == lastPrint) continue;

                    Debug.WriteLine($"Handling: {print}%");
                    lastPrint = print;
                }
            } catch(EndOfStreamException) { }
        }

        public GaitupData GetData()
        {
            if (_data == null) ParseFile();

            return _data;
        }
    }
}
