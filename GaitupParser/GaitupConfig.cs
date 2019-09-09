using System;

namespace GaitupParser
{
    public class GaitupConfig
    {
        public GaitupConfig()
        {
            Accelerometer.Active = false;
            Gyro.Active = false;
            Barometer.Active = false;
            Ble = new GaitupBleConfig(false);
        }
        public DateTime StartDate { get; set; }
        public DateTime StopDate { get; set; }

        public GaitupBleConfig Ble;
        public GaitupRadio Radio;
        public GaitupSensor Sensor;
        public GaitupInertialConfig Accelerometer;
        public GaitupInertialConfig Gyro;
        public BaroConfig Barometer;
        public ushort Frequency;
        public ushort MeasureId;
        public ButtonConfig Button;
    }

    public struct GaitupBleConfig
    {
        public GaitupBleConfig(bool active)
        {
            Active = active;
            SyncFirstPacket = new byte[5];
            SyncLastPacket = new byte[5];
            Id = 0;
            PayloadLength = 0;
            TimestampFirstPacket = 0;
            TimestampLastPacket = 0;
        }
        public bool Active;
        public byte Id;
        public ushort PayloadLength;
        public byte[] SyncFirstPacket;
        public uint TimestampFirstPacket;
        public byte[] SyncLastPacket;
        public uint TimestampLastPacket;

    }

    public struct GaitupRadio
    {
        public bool Active;
        public byte Id;
        public ushort PayloadLength;
        public byte Mode;
        public byte Channel;
    }

    public struct ButtonConfig
    {
        public bool Active;
        public byte Id;
        public ushort PayloadLength;
    }

    public struct GaitupInertialConfig
    {
        public bool Active;
        public byte Id;
        public ushort SamplingFrequency;
        public uint Scale;
        public double OffsetX;
        public double OffsetY;
        public double OffsetZ;
        public double GainX;
        public double GainY;
        public double GainZ;
        public ushort PayloadLength;
        public uint NumberOfSamples;
    }

    public struct BaroConfig
    {
        public bool Active;
        public byte Id;
        public ushort SamplingFrequency;
        public ushort PayloadLength;
        public uint NumberOfSamples;
    }

    public struct GaitupSensor
    {
        public uint DeviceId;
        public byte DeviceType;
        public byte BodyLocation;
        public ushort Version;
        public ushort MajorVersion;
        public ushort MinorVersion;
    }
}
