using System;
using System.Collections.Generic;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Plugins.Import
{
    [PluginType(AllowMultipleImplementations = true, UseSingletonInstance = true)]
    public interface IImportPlugin
    {
        Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory, Dictionary<string, object> parameters);
        void GetExtraConfigurationParameters(Dictionary<string, (object, string)> parameters);
    }

    public class ImportPluginAttribute : PluginAttribute
    {
        public ImportPluginAttribute(string extension) : base(typeof(IImportPlugin), extension) { }
    }

    public static class ImportPlugins
    {
        public static Dictionary<string, List<Type>> ExtensionTypes => PluginService.GetExtensionTypes<IImportPlugin>();
        public static Dictionary<Type, List<string>> TypeExtensions => PluginService.GetTypeExtensions<IImportPlugin>();
    }
}
