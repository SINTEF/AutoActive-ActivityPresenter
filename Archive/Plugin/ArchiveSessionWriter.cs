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

        public string StoreMeta(JObject meta)
        {
            var path = $"{_id}/{ArchiveSession.SessionFileName}";

            var ms = new MemoryStream();

            var writer = new StreamWriter(ms);
            var jsonWriter = new JsonTextWriter(writer);
            var serializer = new JsonSerializer();
            serializer.Serialize(jsonWriter, meta);
            jsonWriter.Flush();
            

            ms.Position = 0;

            _zipFile.BeginUpdate();

            var ss = new StreamSource(ms);
            _zipFile.Add(ss, path, CompressionMethod.Stored);

            _zipFile.CommitUpdate();
            ms.Close();
            ms.Dispose();

            return path;
        }

        public void StoreFileId(Stream data, string path)
        {
            var fullPath = $"{_id}{path}";

            _zipFile.BeginUpdate();

            var ss = new StreamSource(data);
            _zipFile.Add(ss, fullPath, CompressionMethod.Stored);

            _zipFile.CommitUpdate();
            data.Close();
            data.Dispose();
        }

    }
}
