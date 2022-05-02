using System;
using System.Collections.Generic;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Plugins;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Table;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.Plugins.Import.Garmin;
using SINTEF.AutoActive.Plugins.Import.Mqtt;
using SINTEF.AutoActive.Plugins.Import.Video;
using SINTEF.AutoActive.Plugins.Import.Csv;
using SINTEF.AutoActive.Plugins.Import.Gaitup;
using SINTEF.AutoActive.Plugins.Import.Excel;
using SINTEF.AutoActive.Plugins.Import.Json;
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
            typeof(AnnotationProvider),

            // Import plugins
            typeof(GarminImportPlugin),
            typeof(MqttImportPlugin),
            typeof(ImportVideoPlugin),
            typeof(ImportGenericCsv),
            typeof(ImportGenericTxt),
            typeof(ImportGaitupPlugin),
            typeof(ImportCsvCatapult),
            typeof(ImportGenericExcel),
            typeof(ImportGaitUpReults),
            typeof(ImportAnnotationPlugin)
        };
    }
}
