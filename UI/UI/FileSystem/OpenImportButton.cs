using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins;
using SINTEF.AutoActive.Plugins.Import;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.FileSystem
{
    public class OpenImportButton : Button
    {
        public OpenImportButton()
        {
            Text = "Import file";

            IsVisible = CanBrowseForFile();

            Clicked += OpenImportButton_Clicked;
        }

        private async void OpenImportButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var browser = DependencyService.Get<IFileBrowser>();
                var file = await browser?.BrowseForImportFile();
                if (file != null)
                {
                    // FIXME: This should probably be handled somewhere else?
                    // FIXME: This should also handle a case where multiple importers are possible
                    // TODO: Should probably be run on a background thread...

                    // Find the proper import plugin to use
                    var plugins = PluginService.GetAll<IImportPlugin>(file.Extension);

                    var provider = await plugins[0].Import(file);

                    provider?.Register();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR IMPORTING FILE: {ex.Message} \n{ex}");
            }
        }

        public static bool CanBrowseForFile()
        {
            var browser = DependencyService.Get<IFileBrowser>();
            return browser != null;
        }
    }
}
