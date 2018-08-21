using System;
using System.IO;
using System.Collections.Generic;

using ICSharpCode.SharpZipLib.Zip;

namespace SINTEF.AutoActive.Archive
{
    public class Reader
    {
        ISeekableStreamFactory _file;
        ZipFile _zip;
        IDictionary<string, Entry> _entries;

        public Reader(ISeekableStreamFactory file)
        {
            _file = file;
            _entries = new Dictionary<string, Entry>();

            // Parse the zip-file
            _zip = new ZipFile(createStream());

            // Find all entries that we can read directly
            foreach (ZipEntry entry in _zip)
            {
                if (entry.CompressionMethod == CompressionMethod.Stored)
                {
                    // Calculate the offset within the file to the contents of this entry
                    var nameLength = ZipStrings.ConvertToArray(entry.Flags, entry.Name).Length;
                    var extraLength = entry.ExtraData == null ? 0 : entry.ExtraData.Length;
                    var offset = ZipConstants.LocalHeaderBaseSize + entry.Offset + nameLength + extraLength;

                    // Store it for later retreival
                    _entries[entry.Name] = new Entry { Name = entry.Name, Offset = offset, Size = entry.Size };
                }
            }
        }


        /* --- Helpers --- */
        Stream createStream()
        {
            Stream stream = _file.GetStream();
            if (stream == null)
            {
                throw new IOException("Could not create stream from file");
            }
            if (!stream.CanRead)
            {
                throw new IOException("Stream is not readable");
            }
            if (!stream.CanSeek)
            {
                throw new IOException("Stream must be seekable");
            }
            return stream;
        }

        struct Entry
        {
            internal string Name;
            internal long Offset;
            internal long Size;
        }

        class BoundedReadStream : Stream
        {
            Stream _original;
            long _start;
            long _end;
            long _size;

            internal BoundedReadStream(Stream original, long offset, long size)
            {
                _original = original;
                _start = offset;
                _end = offset + size;
                _size = size;
            }


            public override bool CanRead => _original.CanRead;

            public override bool CanSeek => _original.CanSeek;

            public override bool CanWrite => false;

            public override long Length => _size;

            public override long Position
            {
                get => _original.Position - _start;
                set
                {
                    var newPosition = value + _start;
                    if (newPosition < _start) throw new ArgumentException("Negative position is not allowed");
                    if (newPosition >= _end) throw new InvalidOperationException("Cannot seek past end");
                    _original.Position = newPosition;
                }
            }

            public override void Flush() { }

            public override int ReadByte()
            {
                if (_original.Position >= _end) return -1;
                else return base.ReadByte();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // Limit the number of bytes to the ones left in the bounded stream
                var bytesLeft = _end - _original.Position;
                if (count > bytesLeft)
                {
                    count = (int)bytesLeft;
                }
                return _original.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                var newPosition = Position + _start;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        newPosition = _start + offset;
                        break;
                    case SeekOrigin.Current:
                        newPosition += offset;
                        break;
                    case SeekOrigin.End:
                        newPosition = _end + offset;
                        break;
                }
                if (newPosition < _start) throw new ArgumentException("Negative position is not allowed");
                if (newPosition >= _end) throw new InvalidOperationException("Cannot seek past end");
                return _original.Seek(newPosition, SeekOrigin.Begin) - _start;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
