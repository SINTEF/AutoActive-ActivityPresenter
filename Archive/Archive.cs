using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Plugins;

namespace SINTEF.AutoActive.Archive
{
    public class Archive
    {
        private readonly ZipFile _zipFile;
        private readonly IReadSeekStreamFactory _streamFactory;
        private readonly List<ArchiveSession> _sessions = new List<ArchiveSession>();

        /* ---------- Open an existing archive ---------- */
        private Archive(ZipFile file, IReadSeekStreamFactory factory)
        {
            _zipFile = file;
            _streamFactory = factory;
        }

        public string GetFilename()
        {
            return _streamFactory.Name + _streamFactory.Extension;
        }

        private async Task ParseSessions()
        {
            // Find all sessions in the archive
            foreach (ZipEntry entry in _zipFile)
            {
                if (entry.IsFile && entry.CompressionMethod == CompressionMethod.Stored &&
                    entry.Name.EndsWith(ArchiveSession.SessionFileName))
                {
                    // This is an AutoActive session description file
                    await ParseSessionFile(entry);
                }
            }
        }

        private async Task ParseSessionFile(ZipEntry entry)
        {
            using (var stream = await _zipFile.OpenReadSeekStream(entry, _streamFactory))
            using (var streamReader = new StreamReader(stream))

            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var serializer = new JsonSerializer();
                var json = (JToken)serializer.Deserialize(jsonReader);

                var meta = json["meta"];
                if (meta == null)
                {
                    throw new ArgumentException("Object missing 'meta' ", nameof(json));
                }

                var sessionId = meta["id"].ToObject<Guid?>();
                if (!sessionId.HasValue)
                {
                    throw new ArgumentException("Session is missing 'id'");
                }

                var parsedElement = ParseJsonElement(json, sessionId.Value);
                // Load the contents of the file
                if (parsedElement is ArchiveSession session)
                {
                    // If the root object was a session, add it to the list
                    _sessions.Add(session);
                    session.Closing += SessionOnClosing;
                }
            }
        }

        private void SessionOnClosing(object sender, ArchiveSession e)
        {
            _sessions.Remove(e);
            if (!_sessions.Any())
            {
                Close();
            }
        }

        public object ParseJsonElement(JToken json, Guid sessionId)
        {
            // Check if this element is a datastructure
            var meta = (json as JObject)?.Property("meta")?.Value as JObject;
            if (meta?.Property("type") == null)
            {
                return json;
            }

            var type = meta.Property("type").ToObject<string>();
            // Try to parse the object with the specified plugin
            var plugin = PluginService.GetSingle<IArchivePlugin>(type);
            if (plugin != null)
            {
                var parsed = plugin.CreateFromJSON(json as JObject, this, sessionId).Result;
                if (parsed != null) return parsed;
            }
            // If not, try to parse it as a folder (the default)
            plugin = PluginService.GetSingle<IArchivePlugin>(ArchiveFolder.PluginType);
            if (plugin != null)
            {
                var parsed = plugin.CreateFromJSON(json as JObject, this, sessionId).Result;
                if (parsed != null) return parsed;
            }

            // Other handling for "native" JSON objects
            // TODO: Should we do any transformations from the JSON types to native types
            return json;
        }

        public ZipEntry FindFile(string path)
        {
            return _zipFile.GetEntry(path);
        }

        public List<Stream> OpenFiles = new List<Stream>();

        public async Task<BoundedReadSeekStream> OpenFile(ZipEntry entry)
        {
            var stream = await _zipFile.OpenReadSeekStream(entry, _streamFactory);
            OpenFiles.Add(stream);
            return stream;
        }

        public IReadSeekStreamFactory OpenFileFactory(ZipEntry entry)
        {
            return new ArchiveFileBoundFactory(this, entry);
        }

        public static async Task<Archive> Open(IReadSeekStreamFactory file)
        {
            var zipFile = new ZipFile(await file.GetReadStream());
            var archive = new Archive(zipFile, file);
            await archive.ParseSessions();
            return archive;
        }

        /* ---------- Create a new archive from scratch ---------- */
        private Archive(ZipFile zipFile)
        {
            zipFile.UseZip64 = UseZip64.On;
            _zipFile = zipFile;
        }

        public static Archive Create(string fileName)
        {
            return new Archive(ZipFile.Create(fileName));
        }

        public static Archive Create(Stream outStream)
        {
            return new Archive(ZipFile.Create(outStream));
        }

        public void AddSession(ArchiveSession session)
        {
            _sessions.Add(session);
        }

        public async Task WriteFile()
        {
            await WriteFile(_zipFile);
        }

        public async Task WriteFile(string fileName)
        {
            var zip = ZipFile.Create(fileName);
            await WriteFile(zip);
            zip.Close();
        }

        private double _totalProgress = 0d;

        private double SavingProgress
        {
            get => _totalProgress;
            set
            {
                _totalProgress = value;
                SavingProgressChanged?.Invoke(this, _totalProgress);
            }
        }

        public List<(string, Exception)> ErrorList = new List<(string, Exception)>();

        private double _progressStep;
        public async Task WriteFile(ZipFile zipFile)
        {
            SavingProgress = 0d;

            _progressStep = 1d / Sessions.Count;
            var sessionIx = 0;
            foreach (var session in Sessions)
            {
                SavingProgress = _progressStep * sessionIx++;

                session.SavingProgressChanged += SessionOnSavingProgress;
                await session.WriteFile(zipFile);
                if (!session.SavingErrors.Any()) continue;

                foreach (var error in session.SavingErrors)
                {
                    ErrorList.Add((session.Name, error));
                }
            }

            SavingProgress = 1d;
        }


        private void SessionOnSavingProgress(object sender, double progress)
        {
            SavingProgress += progress * _progressStep;
        }

        public void Close()
        {
            _zipFile?.Close();
            foreach (var stream in OpenFiles)
            {
                stream.Close();
            }
        }

        /* ---- Public API ---- */
        public IReadOnlyCollection<ArchiveSession> Sessions => _sessions.AsReadOnly();
        public event EventHandler<double> SavingProgressChanged;

        /* ---- Helpers ---- */
        public class ArchiveFileBoundFactory : IReadSeekStreamFactory
        {
            private readonly Archive _archive;
            private readonly ZipEntry _entry;

            internal ArchiveFileBoundFactory(Archive archive, ZipEntry entry)
            {
                _archive = archive;
                _entry = entry;

                Debug.WriteLine($"ZIPENTRY NAME: {entry.Name}");
            }

            public string Name => throw new NotImplementedException();

            public string Extension => throw new NotImplementedException();

            public string Mime => throw new NotImplementedException();

            public async Task<Stream> GetReadStream()
            {
                return await _archive.OpenFile(_entry);
            }

            public async Task<BoundedReadSeekStream> GetBoundedStream()
            {
                return await _archive.OpenFile(_entry);
            }

            public void Close()
            {
                _archive.Close();
            }
        }
    }
}
