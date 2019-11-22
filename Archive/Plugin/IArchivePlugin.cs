using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

using SINTEF.AutoActive.Plugins;

namespace SINTEF.AutoActive.Archive.Plugin
{
    [PluginType(AllowMultipleImplementations = false, UseSingletonInstance = true)]
    public interface IArchivePlugin
    {
        Task<ArchiveStructure> CreateFromJSON(JObject json, Archive archive, Guid sessionId);
    }

    public class ArchivePluginAttribute : PluginAttribute
    {
        public ArchivePluginAttribute(string type, int priority = 100) : base(typeof(IArchivePlugin), type, priority) { }
    }
}
