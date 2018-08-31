using System;
using System.Collections.Generic;
using System.Text;

using Xamarin.Forms;

using SINTEF.AutoActive.Archive;
using SINTEF.AutoActive.FileSystem;

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
            var browser = DependencyService.Get<IFileBrowser>();
            var file = await browser?.BrowseForArchive();
            if (file != null)
            {
                await Archive.Archive.Open(file);
            }
        }

        public static bool CanBrowseForFile()
        {
            var browser = DependencyService.Get<IFileBrowser>();
            return browser != null;
        }
    }
}
