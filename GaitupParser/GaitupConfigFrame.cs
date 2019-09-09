using System;
using System.Diagnostics;
using System.IO;

namespace GaitupParser
{
    internal abstract class GaitupConfigFrame
    {
        private const int MaxSize = 512;

        private const byte SyncByte1 = (byte)'P';
        private const byte SyncByte2 = (byte)'5';
        private const byte ChecksumByte1 = 0xff;
        private const byte ChecksumByte2 = 0xfe;
        
        public byte Class;
        public byte Id;
        public byte Size;
        public bool Valid;

        internal abstract void ParseFrame(GaitupConfig config, BinaryReader reader);

        internal static bool SyncFrame(BinaryReader reader)
        {
            var startPosition = reader.BaseStream.Position;
            try
            {
                for (int i = 0; i < MaxSize; i++)
                {
                    var byte1 = reader.ReadByte();
                    if (byte1 != SyncByte1)
                    {
                        continue;
                    }
                    if (reader.PeekChar() == SyncByte2)
                    {
                        reader.ReadByte();
                        return true;
                    }
                }
            } catch(EndOfStreamException) { }
            reader.BaseStream.Position = startPosition;
            return false;
        }

        public static GaitupConfigFrame NextFrame(GaitupConfig config, BinaryReader reader)
        {
            if(!SyncFrame(reader))
            {
                return null;
            }

            var classNum = reader.ReadByte();
            var idNum = reader.ReadByte();
            var size = reader.ReadByte();

            GaitupConfigFrame frame = SelectFrame(classNum, idNum);

            frame.Class = classNum;
            frame.Id = idNum;
            frame.Size = size;
            frame.ParseFrame(config, reader);
            frame.Valid = reader.ReadByte() == ChecksumByte1 && reader.ReadByte() == ChecksumByte2;

            return frame;
        }

        private static GaitupConfigFrame SelectFrame(byte classNum, byte idNum)
        {
            switch (classNum)
            {
                case 1:
                    switch (idNum)
                    {
                        case 1:
                            return new GaitupDeviceInfo();
                        case 2:
                            return new GaitupFirmwareInfo();
                    }
                    break;
                case 3:
                    switch (idNum)
                    {
                        case 0:
                            return new GaitupDateFrame((config, val) => config.StartDate = val);
                        case 2:
                            return new GaitupAccelConfigFrame();
                        case 3:
                            return new GaitupGyroConfigFrame();
                        case 4:
                            return new GaitupBaroConfigFrame();
                        case 6:
                            return new GaitupDateFrame((config, val) => config.StopDate = val);
                        case 8:
                            return new GaitupBaseFrequencyFrame();
                        case 11:
                            return new NumberOfSamplesFrame((config, val) => config.Accelerometer.NumberOfSamples = val);
                        case 12:
                            return new NumberOfSamplesFrame((config, val) => config.Gyro.NumberOfSamples = val);
                        case 13:
                            return new NumberOfSamplesFrame((config, val) => config.Barometer.NumberOfSamples = val);
                        case 16:
                            return new ButtonEventFrame();
                        case 17:
                            return new RadioSyncInfoFrame();
                        case 18:
                            return new BleSyncInfoFrame();
                        case 21:
                            return new MeasureIdFrame();
                    }
                    break;
            }
            Debug.WriteLine($"Unknown frame: {classNum}.{idNum}");
            return new GaitupRawFrame();
        }
    }

    internal class BleSyncInfoFrame : GaitupConfigFrame
    {
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 21) throw new InvalidDataException();

            config.Ble.Id = reader.ReadByte();
            config.Ble.PayloadLength = reader.ReadUInt16();
            config.Ble.SyncFirstPacket[0] = reader.ReadByte();
            config.Ble.SyncFirstPacket[1] = reader.ReadByte();
            config.Ble.SyncFirstPacket[2] = reader.ReadByte();
            config.Ble.SyncFirstPacket[3] = reader.ReadByte();
            config.Ble.SyncFirstPacket[4] = reader.ReadByte();
            config.Ble.TimestampFirstPacket = reader.ReadUInt32();
            config.Ble.SyncLastPacket[0] = reader.ReadByte();
            config.Ble.SyncLastPacket[1] = reader.ReadByte();
            config.Ble.SyncLastPacket[2] = reader.ReadByte();
            config.Ble.SyncLastPacket[3] = reader.ReadByte();
            config.Ble.SyncLastPacket[4] = reader.ReadByte();
            config.Ble.TimestampLastPacket = reader.ReadUInt32();

            config.Ble.Active = true;
        }
    }

    internal class RadioSyncInfoFrame : GaitupConfigFrame
    {
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 5) throw new InvalidDataException();

            config.Radio.Id = reader.ReadByte();
            config.Radio.PayloadLength = reader.ReadUInt16();
            config.Radio.Mode = reader.ReadByte();
            config.Radio.Channel = reader.ReadByte();

            config.Radio.Active = true;
        }
    }

    internal class MeasureIdFrame : GaitupConfigFrame
    {
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 2) throw new InvalidDataException();

            config.MeasureId = reader.ReadUInt16();
        }
    }

    internal class ButtonEventFrame : GaitupConfigFrame
    {
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 3) throw new InvalidDataException();

            config.Button.Id = reader.ReadByte();
            config.Button.PayloadLength = reader.ReadUInt16();

            config.Button.Active = true;
        }
    }

    internal class GaitupBaroConfigFrame : GaitupConfigFrame
    {
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 5) throw new InvalidDataException();

            config.Barometer.Active = true;
            config.Barometer.Id = reader.ReadByte();
            config.Barometer.SamplingFrequency = reader.ReadUInt16();
            config.Barometer.PayloadLength = reader.ReadUInt16();
        }
    }

    internal class NumberOfSamplesFrame : GaitupConfigFrame
    {
        private Action<GaitupConfig, uint> _configSetter;

        public NumberOfSamplesFrame(Action<GaitupConfig, uint> configSetter)
        {
            _configSetter = configSetter;
        }

        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 4) throw new InvalidDataException();

            _configSetter(config, reader.ReadUInt32());
        }
    }

    internal class GaitupBaseFrequencyFrame : GaitupConfigFrame
    {
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 2) throw new InvalidDataException();

            config.Frequency = reader.ReadUInt16();
        }
    }

    internal class GaitupDeviceInfo : GaitupConfigFrame
    {
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 6) throw new InvalidDataException();

            config.Sensor.DeviceId = reader.ReadUInt32();
            config.Sensor.DeviceType = reader.ReadByte();
            config.Sensor.BodyLocation = reader.ReadByte();
        }
    }

    internal class GaitupFirmwareInfo : GaitupConfigFrame
    {
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 6) throw new InvalidDataException();

            config.Sensor.Version = reader.ReadUInt16();
            config.Sensor.MajorVersion = reader.ReadUInt16();
            config.Sensor.MinorVersion = reader.ReadUInt16();
        }
    }

    internal class GaitupDateFrame : GaitupConfigFrame
    {
        private Action<GaitupConfig, DateTime> _configSetter;
        public GaitupDateFrame(Action<GaitupConfig, DateTime> setter)
        {
            _configSetter = setter;
        }
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 7) throw new InvalidDataException();

            var second = reader.ReadByte();
            var minute = reader.ReadByte();
            var hour = reader.ReadByte();
            var day = reader.ReadByte();
            var month = reader.ReadByte();
            var year = reader.ReadUInt16();

            _configSetter(config, new DateTime(year, month, day, hour, minute, second));
        }
    }

    internal class GaitupAccelConfigFrame : GaitupConfigFrame
    {
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 30) throw new InvalidDataException();

            config.Accelerometer.Id = reader.ReadByte();
            config.Accelerometer.SamplingFrequency = reader.ReadUInt16();
            var scale = reader.ReadByte();
            switch (scale) {
                case 0:
                    config.Accelerometer.Scale = 2;
                    break;
                case 1:
                    config.Accelerometer.Scale = 16;
                    break;
                case 2:
                    config.Accelerometer.Scale = 4;
                    break;
                case 3:
                    config.Accelerometer.Scale = 8;
                    break;
                default:
                    config.Accelerometer.Scale = 4;
                    Debug.WriteLine($"Unknown accel scale id {scale}, setting to {config.Accelerometer.Scale}");
                    break;
            }
            
            config.Accelerometer.OffsetX = reader.ReadInt32() / 10000.0;
            config.Accelerometer.OffsetY = reader.ReadInt32() / 10000.0;
            config.Accelerometer.OffsetZ = reader.ReadInt32() / 10000.0;
            config.Accelerometer.GainX = reader.ReadInt32() / 10000.0;
            config.Accelerometer.GainY = reader.ReadInt32() / 10000.0;
            config.Accelerometer.GainZ = reader.ReadInt32() / 10000.0;
            config.Accelerometer.PayloadLength = reader.ReadUInt16();

            config.Accelerometer.Active = true;
        }
    }

    internal class GaitupGyroConfigFrame : GaitupConfigFrame
    {
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            if (Size != 30) throw new InvalidDataException();

            config.Gyro.Id = reader.ReadByte();
            config.Gyro.SamplingFrequency = reader.ReadUInt16();
            var scale = reader.ReadByte();
            switch (scale)
            {
                case 0:
                    config.Gyro.Scale = 245;
                    break;
                case 1:
                    config.Gyro.Scale = 500;
                    break;
                case 2:
                    config.Gyro.Scale = 1000;
                    break;
                case 3:
                    config.Gyro.Scale = 2000;
                    break;
                default:
                    config.Gyro.Scale = 1000;
                    Debug.WriteLine($"Unknown gyro scale id {scale}, setting to {config.Gyro.Scale}");
                    
                    break;
            }

            config.Gyro.OffsetX = reader.ReadInt32() / 10000.0;
            config.Gyro.OffsetY = reader.ReadInt32() / 10000.0;
            config.Gyro.OffsetZ = reader.ReadInt32() / 10000.0;
            config.Gyro.GainX = reader.ReadInt32() / 10000.0;
            config.Gyro.GainY = reader.ReadInt32() / 10000.0;
            config.Gyro.GainZ = reader.ReadInt32() / 10000.0;
            config.Gyro.PayloadLength = reader.ReadUInt16();

            config.Gyro.Active = true;
        }
    }

    internal class GaitupRawFrame : GaitupConfigFrame
    {
        public byte[] RawData;
        internal override void ParseFrame(GaitupConfig config, BinaryReader reader)
        {
            RawData = reader.ReadBytes(Size);
        }
    }
}