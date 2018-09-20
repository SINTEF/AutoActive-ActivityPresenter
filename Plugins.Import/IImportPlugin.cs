using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.FileSystem;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.Plugins.Import
{
    [PluginType(AllowMultipleImplementations = true, UseSingletonInstance = true)]
    public interface IImportPlugin
    {
        Task<IDataProvider> Import(IReadSeekStreamFactory readerFactory);
    }

    public class ImportPluginAttribute : PluginAttribute
    {
        public ImportPluginAttribute(string extension) : base(typeof(IImportPlugin), extension) { }
    }

    public static class ImportPlugins
    {
        public static string[] SupportedExtensions
        {
            get => PluginService.GetKinds<IImportPlugin>();
        }
    }
}
