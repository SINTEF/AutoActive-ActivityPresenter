using System;
using System.Diagnostics;
using System.IO;
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

        public string StoreMeta(JObject meta)
        {
            var path = $"{_id}/{ArchiveSession.SessionFileName}";

            using (var ms = new MemoryStream())
            using (var writer = new StreamWriter(ms))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, meta);
                jsonWriter.Flush();

                ms.Position = 0;

                _zipFile.BeginUpdate();
                _zipFile.Add(new StreamSource(ms), path, CompressionMethod.Stored);
                _zipFile.CommitUpdate();
            }

            return path;
        }

        public string StoreFile(Stream data, string name)
        {
            var path = $"{RootName}/{name}";
            _zipFile.BeginUpdate();
            _zipFile.Add(new StreamSource(data), path, CompressionMethod.Stored);
            _zipFile.CommitUpdate();
            return path;
        }

        public void CreateDirectory(string name)
        {
            _zipFile.BeginUpdate();
            _zipFile.AddDirectory($"{RootName}/{name}");
            _zipFile.CommitUpdate();
        }
    }
}
