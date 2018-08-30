using System;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;

using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Archive
{
    public static class ZipExtensions
    {
        private static int ReadLEUshort(Stream stream)
        {
            int data1 = stream.ReadByte();
            if (data1 < 0) throw new EndOfStreamException();
            int data2 = stream.ReadByte();
            if (data2 < 0) throw new EndOfStreamException();
            return data1 | data2 << 8;
        }

        public static async Task<BoundedReadSeekStream> OpenReadSeekStream(this ZipFile zip, ZipEntry entry, IReadSeekStreamFactory factory)
        {
            // Check that the entry is not compressed
            if (entry.CompressionMethod != CompressionMethod.Stored) throw new IOException("Can read compressed entries directly");
            // Create a new stream to read from
            var stream = await factory.GetReadStream();
            // Locate the start of the content from the local file header manually
            stream.Seek(entry.Offset, SeekOrigin.Begin); // NOTE: There is something about disks here, but I hope that is not an issue
            stream.Seek(26, SeekOrigin.Current);
            var nameLength = ReadLEUshort(stream);
            var extraLength = ReadLEUshort(stream);
            var offset = entry.Offset + 30 + nameLength + extraLength;
            // Return the bounded stream to that content
            return new BoundedReadSeekStream(stream, offset, entry.Size);
        }
    }

    public class BoundedReadSeekStream : Stream
    {
        readonly Stream stream;
        readonly long start;
        readonly long end;
        readonly long length;

        internal BoundedReadSeekStream(Stream original, long offset, long size)
        {
            // We know the stream is readable and seekable (from above) so no need to check that
            stream = original;
            start = offset;
            end = offset + size;
            length = size;
            Seek(0, SeekOrigin.Begin);
            Position = 0;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position
        {
            get => stream.Position - start;
            set
            {
                var newPosition = value + start;
                if (newPosition < start) throw new ArgumentException("Negative position is not allowed");
                if (newPosition >= end) throw new InvalidOperationException("Cannot seek past end");
                stream.Position = newPosition;
            }
        }

        public override void Flush()
        {
            // Does nothing
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Limit the number of bytes to the ones left in the bounded stream
            var bytesLeft = end - stream.Position;
            if (count > bytesLeft)
            {
                count = (int)bytesLeft;
            }
            return stream.Read(buffer, offset, count);
        }

        public override int ReadByte()
        {
            if (stream.Position >= end) return -1;
            else return base.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosition = Position + start;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = start + offset;
                    break;
                case SeekOrigin.Current:
                    newPosition += offset;
                    break;
                case SeekOrigin.End:
                    newPosition = end + offset;
                    break;
            }
            if (newPosition < start) throw new ArgumentException("Negative position is not allowed");
            if (newPosition >= end) throw new InvalidOperationException("Cannot seek past end");
            return stream.Seek(newPosition, SeekOrigin.Begin) - start;
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }
    }
}
