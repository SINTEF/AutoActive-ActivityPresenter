using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

using SINTEF.AutoActive.Databus;

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
                var datastruct = content as ArchiveStructure;
                if (datastruct != null)
                {
                    datastruct.SetName(property.Name);
                    AddChild(datastruct);
                }
            }
        }

        /*
        protected override void ToArchiveJSON(JObject meta, JObject user)
        {
            throw new NotImplementedException();
        }
        */

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
