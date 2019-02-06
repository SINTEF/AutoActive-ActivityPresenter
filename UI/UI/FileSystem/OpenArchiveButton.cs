using System;
using System.Collections.Generic;
using System.Text;

using Xamarin.Forms;

using SINTEF.AutoActive.Archive;
using SINTEF.AutoActive.FileSystem;
using System.Diagnostics;
using SINTEF.AutoActive.Databus;
using System.Threading.Tasks;

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
            if (browser == null)
            {
                await Application.Current.MainPage.DisplayAlert("Open error", "Could get file browser.", "OK");
                return;
            }

            var file = await browser.BrowseForArchive();
            if (file == null) return;

            try
            {
                // Load Archive in the background
                var archive = await Task.Run(() => Archive.Archive.Open(file));
                foreach (var session in archive.Sessions)
                {
                    session.Register();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR OPENING ARCHIVE: {ex.Message} \n{ex}");
                await Application.Current.MainPage.DisplayAlert("Open error", $"Could not open archive:\n{ex.Message}", "OK");
            }
        }

        public static bool CanBrowseForFile()
        {
            var browser = DependencyService.Get<IFileBrowser>();
            return browser != null;
        }
    }
}
