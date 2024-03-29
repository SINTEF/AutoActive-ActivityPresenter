﻿using System;
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
        Task<bool> CanParse(IReadSeekStreamFactory readerFactory);
    }

    public interface IBatchImportPlugin : IImportPlugin
    {
        void StartTransaction(List<IReadSeekStreamFactory> streamFactories);
        void EndTransaction();
    }

    public class ImportPluginAttribute : PluginAttribute
    {
        public ImportPluginAttribute(string extension, int priority = DefaultPriority) : base(typeof(IImportPlugin), extension, priority) { }
    }

    public static class ImportPlugins
    {
        public static Dictionary<string, List<Type>> ExtensionTypes => PluginService.GetExtensionTypes<IImportPlugin>();
        public static Dictionary<Type, List<string>> TypeExtensions => PluginService.GetTypeExtensions<IImportPlugin>();
    }
}
