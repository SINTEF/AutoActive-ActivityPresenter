using SINTEF.AutoActive.FileSystem;
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
                    Debug.WriteLine($"IMPORTING FILE!");
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
