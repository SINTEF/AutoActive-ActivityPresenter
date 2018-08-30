using System;
using Newtonsoft.Json.Linq;

using SINTEF.AutoActive.Plugins.Registry;

namespace SINTEF.AutoActive.Archive.Plugin
{
    public interface IArchivePlugin
    {
        ArchiveStructure CreateFromJSON(JObject json, Archive archive);
    }

    public class ArchivePluginAttribute : PluginAttribute
    {
        public ArchivePluginAttribute(Type implementorType, string name)
            : base(typeof(IArchivePlugin), implementorType, name)
        { }
    }
}
