using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Plugins;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Table;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.Plugins.Import.Garmin;
using System;
using System.Collections.Generic;
using System.Text;
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
            typeof(ArchiveVideoPlugin), // FIXME: Uncomment when implemented

            // Import plugins
            typeof(GarminImportPlugin),
        };
    }
}
