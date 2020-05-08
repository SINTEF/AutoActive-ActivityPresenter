using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.FileSystem;
using SINTEF.AutoActive.Plugins;
using SINTEF.AutoActive.Plugins.Import;
using SINTEF.AutoActive.UI.Pages;
using SINTEF.AutoActive.UI.Pages.Player;
using SINTEF.AutoActive.UI.Pages.HeadToHead;
using SINTEF.AutoActive.UI.Pages.Synchronization;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views
{
	public partial class CustomNavigationBar : ContentView
	{
        public static readonly GridLength DefaultHeight = 40;
        private readonly IFileBrowser _browser;

        public CustomNavigationBar()
        {
            InitializeComponent();

            _browser = DependencyService.Get<IFileBrowser>();
            if (_browser == null)
            {
                XamarinHelpers.GetCurrentPage(Navigation).DisplayAlert("Critical error", "Could get file browser. Will not be able to open and save files.", "OK");
            }
        }

        // Open archive button
	    private async void OpenArchiveButton_OnClicked(object sender, EventArgs e)
	    {
	        var file = await _browser.BrowseForLoad();
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
                await XamarinHelpers.GetCurrentPage().DisplayAlert("Open error", $"Could not open archive:\n{ex.Message}", "OK");
	        }
        }

        // Import files handling
        private static async void DoImportFiles(IEnumerable<IReadSeekStreamFactory> files)
        {
            var pluginPages = new Dictionary<IImportPlugin, (List<IReadSeekStreamFactory>, ImportParametersPage)>();

            foreach (var file in files)
            {
                var ext = file.Extension.ToLower();
                // FIXME: This should probably be handled somewhere else?
                // FIXME: This should also handle a case where multiple importers are possible
                // TODO: Should probably be run on a background thread...

                // Find the proper import plugin to use
                var plugins = PluginService.GetAll<IImportPlugin>(ext);

                if (!plugins.Any())
                {
                    await XamarinHelpers.GetCurrentPage().DisplayAlert("Import error", $"Could not find a plugin for extension \"{ext}\" files", "OK");
                    continue;
                }

                foreach (var plugin in plugins)
                {
                    try
                    {
                        if(!(await plugin.CanParse(file)))
                        {
                            continue;
                        }
                    } catch(Exception ex)
                    {
                        await ShowError(file.Name, ex);
                    }
                    try
                    {
                        List<IReadSeekStreamFactory> streamFactoryList;

                        if (!pluginPages.TryGetValue(plugin, out var listPage))
                        {
                            var parameters = new Dictionary<string, (object, string)>
                            {
                                ["Name"] = ("$filename", "Name of the imported session file.\nThe text $filename is replaced with the file name of the file \nand $fileext is replaced with the file extension.")
                            };

                            plugin.GetExtraConfigurationParameters(parameters);
                            var page = new ImportParametersPage($"{file.Name}{file.Extension}", parameters);
                            await XamarinHelpers.GetCurrentPage().Navigation.PushAsync(page: page);

                            streamFactoryList = new List<IReadSeekStreamFactory>();
                            pluginPages[plugin] = (streamFactoryList, page);
                        }
                        else
                        {
                            streamFactoryList = listPage.Item1;
                        }

                        streamFactoryList.Add(file);
                        break;
                    }
                    catch (Exception ex)
                    {
                        await ShowError(file.Name, ex);
                    }
                }
            }

            // Start import for each plugin and signal batch import if implemented
            foreach (var pluginItem in pluginPages)
            {
                var plugin = pluginItem.Key;
                var fileList = pluginItem.Value.Item1;
                var page = pluginItem.Value.Item2;
                var bip = plugin as IBatchImportPlugin;

                bip?.StartTransaction(fileList);

                foreach (var file in fileList)
                {
                    page.Disappearing += async (s, a) =>
                    {
                        try
                        {
                            var fileParams = page.Parameters;
                            fileParams["Name"] = (fileParams["Name"] as string)?.Replace("$fileext", file.Extension)
                                .Replace("$filename", file.Name);
                            var provider = await plugin.Import(file, fileParams);
                            provider?.Register();
                        }
                        catch (Exception ex)
                        {
                            await ShowError(file.Name, ex);
                        }
                    };
                }

                if (bip != null)
                {
                    page.Disappearing += (s, a) => bip.EndTransaction();
                }
            }
        }

        // Show error message
        private static async Task ShowError(string filename, Exception ex)
        {
            Debug.WriteLine($"Could not import file {filename}: {ex.Message}");
            await Application.Current.MainPage.DisplayAlert("Open error",
                $"Could not import file \"{filename}\":\n{ex.Message}", "OK");
        }

        // Import button
        private async void OpenImportButton_OnClicked(object sender, EventArgs e)
        {
            var files = await _browser.BrowseForImportFiles();

            if (files == null) return;

            XamarinHelpers.EnsureMainThread(() => DoImportFiles(files));
        }

        // Store one instance of each page to keep setup and content for each page
        private static SavingPage _savingPage;
        private static PointSynchronizationPage   _syncPage;
        private static HeadToHead _headToHeadPage;

        // Main page button
        private void PlayerPage_OnClicked(object sender, EventArgs e)
        {
            // Check if new page is same as current, avoid reopening same page
            if (Navigation.NavigationStack.Count == 0 ||
                XamarinHelpers.GetCurrentPage(Navigation).GetType() != typeof(PlayerPage))
            {
                // Check if current page is saving page
                if (XamarinHelpers.GetCurrentPage(Navigation).GetType() == typeof(SavingPage))
                {
                    //Saving page, check if any ongoing operations
                    if (_savingPage.CheckBeforeExit(false))
                    {
                        // Ongoing operation, quit
                        return;
                    }
                }
                // Check if current page is sync page
                if (XamarinHelpers.GetCurrentPage(Navigation).GetType() == typeof(PointSynchronizationPage))
                {
                    //Saving page, check if any ongoing operations
                    if (_syncPage.CheckUnsavedSync())
                    {
                        // Will run pop on page, no further action required
                        return;
                    }
                }
                // Close page as there is no ongoing operations or unsaved data for this page
                XamarinHelpers.EnsureMainThread(async () => await Navigation.PopAsync());
            }
        }

        // Sync page button
        private void SynchronizationButton_OnClicked(object sender, EventArgs e)
	    {
            // Avoid reopening same page as current page
            if (Navigation.NavigationStack.Count == 0 ||
                XamarinHelpers.GetCurrentPage(Navigation).GetType() != typeof(PointSynchronizationPage))
            {
                if (_syncPage == null)
                {
                    _syncPage = new PointSynchronizationPage();
                }
                switch_page(_syncPage);
            }
        }

        // Head to head button
        private void Head2Head_OnClicked(object sender, EventArgs e)
        {
            // Avoid reopening same page as current page
            if (Navigation.NavigationStack.Count == 0 ||
                XamarinHelpers.GetCurrentPage(Navigation).GetType() != typeof(HeadToHead))
            {
                if (_headToHeadPage == null)
                {
                    _headToHeadPage = new HeadToHead();
                }
                switch_page(_headToHeadPage);
            }
        }

        // Save archive button
        public void SaveArchiveButton_OnClicked(object sender, EventArgs e)
        {
            // Avoid reopening same page as current page
            if (Navigation.NavigationStack.Count == 0 ||
                XamarinHelpers.GetCurrentPage(Navigation).GetType() != typeof(SavingPage))
            {
                if (_savingPage == null)
                {
                    _savingPage = new SavingPage();
                }
                switch_page(_savingPage);
            }
        }

        // Method to switch to new page
        // Should not be used when switching to PlayerPage as this is the main page
        // TBD - if an ongoing operation is aborted, it will always switch back to main page.
        private async void switch_page(Page newPage)
        {
            // Check if current page is saving page
            if (XamarinHelpers.GetCurrentPage(Navigation).GetType() == typeof(SavingPage))
            {
                //Saving page, check if any ongoing operations
                if (_savingPage.CheckBeforeExit(false))
                {
                    // ongoing operation, quit change
                    return;
                }
            }
            // Check if current page is Sync page
            if (XamarinHelpers.GetCurrentPage(Navigation).GetType() == typeof(PointSynchronizationPage))
            {
                //Sync page, check if any unsaved sync
                if (_syncPage.CheckUnsavedSync())
                {
                    // unsaved data, will run pop on page
                }
            }

            // Start new page and remove current page
            Page currPage = XamarinHelpers.GetCurrentPage(Navigation);
            await Navigation.PushAsync(newPage);
            // Remove current page unless main page (must keep one (main) page)
            if (currPage.GetType() != typeof(PlayerPage))
            {
                Navigation.RemovePage(currPage);
            }
        }
    }
}