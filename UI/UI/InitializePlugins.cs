using System;
using System.Collections.Generic;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Plugins;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Table;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.Plugins.Import.Garmin;
using SINTEF.AutoActive.Plugins.Import.Mqtt;
using SINTEF.AutoActive.Plugins.Import.Video;
using SINTEF.AutoActive.Plugins.Import.Csv.Catapult;
using SINTEF.AutoActive.Plugins.Import.Gaitup;
using Xamarin.Forms;

[assembly: Dependency(typeof(SINTEF.AutoActive.UI.InitializePlugins))]
namespace SINTEF.AutoActive.UI
{
    public class InitializePlugins : IPluginInitializer
    {
        IEnumerable<Type> IPluginInitializer.Plugins => new [] {
            // Archive plugins
            typeof(ArchiveFolderPlugin),
            typeof(ArchiveSessionPlugin),
            typeof(ArchiveTablePlugin),
            typeof(ArchiveVideoPlugin),

            // Import plugins
            typeof(GarminImportPlugin),
            typeof(MqttImportPlugin),
            typeof(ImportVideoPlugin),
            typeof(CatapultImportPlugin),
            typeof(ImportGaitupPlugin),
        };
    }
}
