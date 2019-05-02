using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Interfaces;

namespace SINTEF.AutoActive.Archive.Plugin
{
    public class ArchiveFolder : ArchiveStructure, ISaveable
    {

        public static string PluginType = "no.sintef.folder";

        public override string Type { get; } = PluginType;

        private readonly Archive _archive;
        private readonly Guid _sourceSessionId;

        public ArchiveFolder(JObject json, Archive archive, Guid sessionId) : base(json)
        {
            // Remember sessionId in case we shall write attachements later
            _archive = archive;
            _sourceSessionId = sessionId;

            // Find all the contents of the folder
            foreach (var property in User.Properties())
            {
                var content = archive.ParseJsonElement(property.Value, sessionId);
                if (!(content is ArchiveStructure datastruct)) continue;

                datastruct.SetName(property.Name);
                AddChild(datastruct);
            }

            //TODO: Verify that this works
            var type = Meta.Property("type");
            if (type != null)
            {
                Type = type.ToObject<string>();
            }

        }

        public static ArchiveFolder Create(Archive archive, Guid sessionId, string name)
        {
            var json = new JObject
            {
                ["meta"] = new JObject
                {
                    ["type"] = PluginType
                },
                ["user"] = new JObject()
            };

            return new ArchiveFolder(json, archive, sessionId) { IsSaved = false, Name = name};
        }

        public bool IsSaved { get; protected set; }

        public async virtual Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            if (Meta.ContainsKey("attachments"))
            {
                // There are attachments...
                var pathArr = Meta["attachments"].ToObject<string[]>();
                foreach (var path in pathArr)
                {
                    // Fetch from sourceArchive
                    var fullSourcePath = "" + _sourceSessionId + path;
                    var zipEntry = _archive.FindFile(fullSourcePath) ?? throw new ZipException($"{Meta["type"]} file '{path}' not found in archive");
                    var stream = await _archive.OpenFile(zipEntry);

                    // Store in new session
                    writer.StoreFileId(stream, path);
                }
            }

            // Copy previous
            root["meta"] = Meta;
            root["user"] = User;

            return true;
        }
    }

    [ArchivePlugin("no.sintef.folder")]
    public class ArchiveFolderPlugin : IArchivePlugin
    {
        public Task<ArchiveStructure> CreateFromJSON(JObject json, Archive archive, Guid sessionId)
        {
            return Task.FromResult<ArchiveStructure>(new ArchiveFolder(json, archive, sessionId));
        }
    }
}
