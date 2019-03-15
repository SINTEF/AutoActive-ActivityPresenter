using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Interfaces;

namespace SINTEF.AutoActive.Archive.Plugin
{
    internal class ArchiveSessionWriter : ISessionWriter
    {
        private class StreamSource : IStaticDataSource
        {
            private readonly Stream _stream;

            public StreamSource(Stream stream)
            {
                _stream = stream;
            }
            public Stream GetSource()
            {
                return _stream;
            }
        }

        private readonly ZipFile _zipFile;
        private readonly Guid _id;

        public bool JsonCreated
        {
            get;
        }

        public ArchiveSessionWriter(ZipFile file, ArchiveSession session)
        {
            _zipFile = file;
            _id = session.Id;
            RootName = _id.ToString();
            JsonCreated = !session.IsSaved;
        }

        public void PushPathName(string name)
        {
            if (name.Length == 0)
            {
                throw new ArgumentException();
            }
            RootName += $"/{name}";
        }

        public void PopPathName()
        {
            RootName = RootName.Substring(0, RootName.LastIndexOf('/'));
        }

        public string RootName { get; private set; }

        private bool _isUpdating;
        public bool BeginUpdate()
        {
            if (_isUpdating) return false;

            _zipFile.BeginUpdate();
            _isUpdating = true;

            return true;
        }

        private readonly LinkedList<Stream> _streams = new LinkedList<Stream>();

        public void CommitUpdate()
        {
            _zipFile.CommitUpdate();
            _isUpdating = false;

            foreach (var stream in _streams)
            {
                stream.Close();
            }
        }

        public string StoreMeta(JObject meta)
        {
            var path = $"{_id}/{ArchiveSession.SessionFileName}";

            var ms = new MemoryStream();
            _streams.AddLast(ms);

            var writer = new StreamWriter(ms);
            var jsonWriter = new JsonTextWriter(writer);
            var serializer = new JsonSerializer();
            serializer.Serialize(jsonWriter, meta);
            jsonWriter.Flush();
            

            ms.Position = 0;

            var changed = BeginUpdate();
            _zipFile.Add(new StreamSource(ms), path, CompressionMethod.Stored);
            if (changed) _zipFile.CommitUpdate();

            return path;
        }

        public string StoreFile(Stream data, string name)
        {
            var path = $"{RootName}/{name}";
            var changed = BeginUpdate();
            _zipFile.Add(new StreamSource(data), path, CompressionMethod.Stored);

            if (changed) CommitUpdate();
            return path;
        }

        public void EnsureDirectory(string name)
        {
            var pathName = $"{RootName}/{name}";
            if (_zipFile.FindEntry(pathName, true) == -1) return;

            var changed = BeginUpdate();
            _zipFile.AddDirectory(pathName);
            if (changed) CommitUpdate();
        }
    }
}
