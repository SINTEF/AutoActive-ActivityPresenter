using System;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GaitupTest")]
namespace GaitupParser
{
    public class BigEndianBinaryReader : BinaryReader
    {
        public BigEndianBinaryReader(Stream stream) : base(stream) { }

        public override short ReadInt16()
        {
            var data = base.ReadBytes(2);
            Array.Reverse(data);
            return BitConverter.ToInt16(data, 0);
        }
        public override ushort ReadUInt16()
        {
            var data = base.ReadBytes(2);
            Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }

        public override int ReadInt32()
        {
            var data = base.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }

        public override uint ReadUInt32()
        {
            var data = base.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }

        public override long ReadInt64()
        {
            var data = base.ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToInt64(data, 0);
        }

        public override ulong ReadUInt64()
        {
            var data = base.ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToUInt64(data, 0);
        }

        public override float ReadSingle()
        {
            var data = base.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToSingle(data, 0);
        }

        public override double ReadDouble()
        {
            var data = base.ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToDouble(data, 0);
        }

        public override decimal ReadDecimal()
        {
            throw new NotImplementedException();
        }

        public static uint GetUInt32(byte[] data, int index)
        {
            return (uint)GetInt32(data, index);
        }
        public static int GetInt32(byte[] data, int index)
        {
            return (data[index + 0] << 24) | (data[index + 1] << 16) |
                (data[index + 2] << 8) | data[index + 3];
        }
        public static short GetInt16(byte[] data, int index)
        {
            return (short)((data[index + 0] << 8) | data[index + 1]);
        }
        public static ushort GetUInt16(byte[] data, int index)
        {
            return (ushort)GetInt16(data, index);
        }
    }
}
