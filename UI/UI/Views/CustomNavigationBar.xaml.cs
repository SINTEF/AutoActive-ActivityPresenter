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

            var versionGetter = DependencyService.Get<IVersionProvider>();

            if (versionGetter != null)
            {
                VersionLabel.Text = versionGetter.Version;
            }
        }

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

                archive.Close();

            }
	        catch (Exception ex)
	        {
	            Debug.WriteLine($"ERROR OPENING ARCHIVE: {ex.Message} \n{ex}");
                await XamarinHelpers.GetCurrentPage().DisplayAlert("Open error", $"Could not open archive:\n{ex.Message}", "OK");
	        }
        }

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

        private static async Task ShowError(string filename, Exception ex)
        {
            Debug.WriteLine($"Could not import file {filename}: {ex.Message}");
            await Application.Current.MainPage.DisplayAlert("Open error",
                $"Could not import file \"{filename}\":\n{ex.Message}", "OK");
        }

        private async void OpenImportButton_OnClicked(object sender, EventArgs e)
        {
            var files = await _browser.BrowseForImportFiles();

            if (files == null) return;

            XamarinHelpers.EnsureMainThread(() => DoImportFiles(files));
        }

        private SavingPage _savingPage;

        private async void SaveArchiveButton_OnClicked(object sender, EventArgs e)
        {
            if (_savingPage == null)
            {
                _savingPage = new SavingPage();
            }

            await Navigation.PushAsync(_savingPage);
        }

	    private void SynchronizationButton_OnClicked(object sender, EventArgs e)
	    {
	        Navigation.PushAsync(new PointSynchronizationPage());
	    }

        private void Head2Head_OnClicked(object sender, EventArgs e)
        {
            Navigation.PushAsync(new HeadToHead());
        }
    }
}