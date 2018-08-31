using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json.Linq;

using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus;

[assembly: ArchivePlugin(typeof(ArchiveFolderPlugin), "no.sintef.folder")]
namespace SINTEF.AutoActive.Archive.Plugin
{
    public class ArchiveFolder : ArchiveStructure
    {
        public static string PluginType = "no.sintef.folder";

        public override string Type => PluginType;

        private readonly List<ArchiveStructure> contents = new List<ArchiveStructure>();

        internal ArchiveFolder(JObject json, Archive archive) : base(json)
        {
            // Find all the contents of the folder
            foreach (var property in User.Properties())
            {
                var content = archive.ParseJSONElement(property.Value);
                var datastruct = content as ArchiveStructure;
                if (datastruct != null)
                {
                    datastruct.Name = property.Name;
                    contents.Add(datastruct);
                }
            }
        }

        protected internal override void RegisterContents(DataStructureAddedToHandler dataStructureAdded, DataPointAddedToHandler dataPointAdded)
        {
            foreach (var content in contents)
            {
                dataStructureAdded?.Invoke(content, this);
                content.RegisterContents(dataStructureAdded, dataPointAdded);
            }
        }

        protected override void ToArchiveJSON(JObject meta, JObject user)
        {
            throw new NotImplementedException();
        }

        
    }

    public class ArchiveFolderPlugin : IArchivePlugin
    {
        public ArchiveStructure CreateFromJSON(JObject json, Archive archive)
        {
            return new ArchiveFolder(json, archive);
        }
    }
}
