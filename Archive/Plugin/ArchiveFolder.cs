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

        public ArchiveFolder(JObject json, Archive archive, Guid sessionId) : base(json)
        {
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

        public virtual Task<bool> WriteData(JObject root, ISessionWriter writer)
        {
            writer.EnsureDirectory(Name);

            // if (!root.TryGetValue("user", out var user))
            // {
            // 
            //     user = new JObject();
            //     root["user"] = user;
            //     root["user"]["name"] = Name;
            // }

            // if (!root.TryGetValue("meta", out var meta))
            // {
            //     meta = new JObject();
            //     root["meta"] = meta;
            // }

            // meta["type"] = Type;

            // Copy previous
            root["meta"] = Meta;
            root["user"] = User;

            return Task.FromResult(true);
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
