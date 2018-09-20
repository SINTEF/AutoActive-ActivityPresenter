using System;
using System.Collections.Generic;
using System.Text;

using Xamarin.Forms;

using SINTEF.AutoActive.Archive;
using SINTEF.AutoActive.FileSystem;
using System.Diagnostics;

namespace SINTEF.AutoActive.UI.FileSystem
{
    public class OpenArchiveButton : Button
    {
        public OpenArchiveButton()
        {
            Text = "Open archive";

            IsVisible = CanBrowseForFile();

            Clicked += OpenArchiveButton_Clicked;
        }

        private async void OpenArchiveButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var browser = DependencyService.Get<IFileBrowser>();
                var file = await browser?.BrowseForArchive();
                if (file != null)
                {
                    // new Plugins.Import.Garmin.GarminImporter("C:\\Users\\steffend\\SINTEF\\AutoActive Internt - Dokumenter\\Data\\2018-05-16 Gange løp\\activity_Trine.tcx");
                    // Plugins.Import.Garmin.GarminImporter.Open(file);
                    await Archive.Archive.Open(file);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR OPENING ARCHIVE: {ex.Message} \n{ex}");
            }
        }

        public static bool CanBrowseForFile()
        {
            var browser = DependencyService.Get<IFileBrowser>();
            return browser != null;
        }
    }
}
