using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Plugins;

namespace SINTEF.AutoActive.Archive
{
    public class Archive : IDataProvider
    {
        public event DataPointAddedToHandler DataPointAddedTo;
        public event DataPointRemovedHandler DataPointRemoved;
        public event DataStructureAddedToHandler DataStructureAddedTo;
        public event DataStructureRemovedHandler DataStructureRemoved;

        ZipFile zipFile;
        IReadWriteSeekStreamFactory streamFactory;
        List<ArchiveSession> sessions;

        /* ---------- Open an existing archive ---------- */
        private Archive(ZipFile file, IReadWriteSeekStreamFactory factory)
        {
            zipFile = file;
            streamFactory = factory;
            sessions = new List<ArchiveSession>();
        }

        private void ParseSessions()
        {
            // Find all sessions in the archive
            foreach (ZipEntry entry in zipFile)
            {
                if (entry.IsFile && entry.CompressionMethod == CompressionMethod.Stored && entry.Name.EndsWith("AUTOACTIVEMETA.json"))
                {
                    // This is an AutoActive session description file
                    ParseSessionFile(entry);
                }
            }
        }

        private async void ParseSessionFile(ZipEntry entry)
        {
            using (var stream = await zipFile.OpenReadSeekStream(entry, streamFactory))
            using (var streamReader = new StreamReader(stream))

            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var serializer = new JsonSerializer();
                var json = (JToken)serializer.Deserialize(jsonReader);

                // Load the contents of the file
                var session = ParseJSONElement(json) as ArchiveSession;
                if (session != null)
                {
                    // If the root object was a session, add it to the list and registry
                    sessions.Add(session);
                    DataStructureAddedTo?.Invoke(session, DataRegistry.RootStructure);
                    // and register the contents
                    session.RegisterContents(DataStructureAddedTo, DataPointAddedTo);
                }
            }
        }

        public object ParseJSONElement(JToken json)
        {
            // Check if this element is a datastructure
            var meta = (json as JObject)?.Property("meta")?.Value as JObject;
            if (meta?.Property("type") != null)
            {
                var type = meta.Property("type").ToObject<string>();
                // Try to parse the object with the specified plugin
                var plugin = PluginService.GetSingle<IArchivePlugin>(type);
                var parsed = plugin?.CreateFromJSON(json as JObject, this);
                if (parsed != null) return parsed;
                // If not, try to parse it as a folder (the default)
                plugin = PluginService.GetSingle<IArchivePlugin>(ArchiveFolder.PluginType);
                parsed = plugin?.CreateFromJSON(json as JObject, this);
                if (parsed != null) return parsed;
            }

            // Other handling for "native" JSON objects
            // TODO: Should we do any transformations from the JSON types to native types
            return json;
        }

        public ZipEntry FindFile(string path)
        {
            return zipFile.GetEntry(path);
        }

        public async Task<Stream> OpenFile(ZipEntry entry)
        {
            return await zipFile.OpenReadSeekStream(entry, streamFactory);
        }

        public IReadSeekStreamFactory OpenFileFactory(ZipEntry entry)
        {
            return new ArchiveFileBoundFactory(this, entry);
        }


        public async static Task<Archive> Open(IReadWriteSeekStreamFactory file)
        {
            var zipFile = new ZipFile(await file.GetReadWriteStream());
            var archive = new Archive(zipFile, file);
            // Register with the Databus to let others know about the data inside
            archive.Register();
            // Parse the Sessions in the archive
            archive.ParseSessions();
            return archive;
        }



        /* ---------- Create a new archive from scratch ---------- */
        private Archive()
        {
            // TODO: Implement
        }

        public static Archive Create()
        {
            throw new NotImplementedException();
        }

        /* ---- Helpers ---- */
        internal class ArchiveFileBoundFactory : IReadSeekStreamFactory
        {
            Archive archive;
            ZipEntry entry;

            internal ArchiveFileBoundFactory(Archive archive, ZipEntry entry)
            {
                this.archive = archive;
                this.entry = entry;

                Debug.WriteLine($"ZIPENTRY NAME: {entry.Name}");
            }

            public string Name => throw new NotImplementedException();

            public string Extension => throw new NotImplementedException();

            public string Mime => throw new NotImplementedException();

            public async Task<Stream> GetReadStream()
            {
                return await archive.OpenFile(entry);
            }
        }
    }
}
