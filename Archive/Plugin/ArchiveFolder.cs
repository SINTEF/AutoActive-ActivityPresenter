using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SINTEF.AutoActive.Archive.Plugin
{
    public class ArchiveFolder : ArchiveStructure
    {
        public static string PluginType = "no.sintef.folder";

        public override string Type => PluginType;

        internal ArchiveFolder(JObject json, Archive archive) : base(json)
        {
            // Find all the contents of the folder
            foreach (var property in User.Properties())
            {
                var content = archive.ParseJSONElement(property.Value);
                if (!(content is ArchiveStructure datastruct)) continue;

                datastruct.SetName(property.Name);
                AddChild(datastruct);
            }
        }
    }

    [ArchivePlugin("no.sintef.folder")]
    public class ArchiveFolderPlugin : IArchivePlugin
    {
        public Task<ArchiveStructure> CreateFromJSON(JObject json, Archive archive)
        {
            return Task.FromResult<ArchiveStructure>(new ArchiveFolder(json, archive));
        }
    }
}
