using SINTEF.AutoActive.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Plugins.Tests
{
    public class TestFileReader : IReadWriteSeekStreamFactory
    {
        protected List<Stream> Streams = new List<Stream>();
        internal TestFileReader(string name)
        {
            Name = name;
        }
        public string Name { get; }
        public string Extension { get; }
        public string Mime { get; }

        public Task<Stream> GetReadStream()
        {
            Stream locStream = null;
            Stream locBuffStream = null;
            try
            {
                locStream = File.Open(Name, FileMode.Open, FileAccess.Read);
                locBuffStream = new BufferedStream(locStream);
                Streams.Add(locBuffStream);
                Streams.Add(locStream);
            }
            catch (Exception)
            {
                locBuffStream?.Close();
                locStream?.Close();
                throw;
            }

            var task = new Task<Stream>(() => locBuffStream);
            task.Start();
            return task;
        }

        public Task<Stream> GetReadWriteStream()
        {
            Stream locStream = null;
            Stream locBuffStream = null;
            try
            {
                locStream = File.Open(Name, FileMode.Open, FileAccess.ReadWrite);
                locBuffStream = new BufferedStream(locStream);
                Streams.Add(locBuffStream);
                Streams.Add(locStream);
            }
            catch (Exception)
            {
                locBuffStream?.Close();
                locStream?.Close();
                throw;
            }

            var task = new Task<Stream>(() => locBuffStream);
            task.Start();
            return task;
        }

        public void Close()
        {
            foreach (var stream in Streams)
            {
                stream.Close();
            }
        }

    }
}
