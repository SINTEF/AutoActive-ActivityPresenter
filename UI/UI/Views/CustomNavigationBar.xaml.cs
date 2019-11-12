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

	    public HashSet<ArchiveSession> OpenSessions = new HashSet<ArchiveSession>();

        private readonly IFileBrowser _browser;

        public CustomNavigationBar()
        {
            InitializeComponent();

            // TODO(sigurdal) should this event be deregistered?
            SaveComplete += OnSaveComplete;

            _browser = DependencyService.Get<IFileBrowser>();
            if (_browser == null)
            {
                XamarinHelpers.GetCurrentPage(Navigation).DisplayAlert("Critical error", "Could get file browser. Will not be able to open and save files.", "OK");
            }
        }

        /* -- Menu Button -- */
        public static readonly BindableProperty MenuButtonShownProperty = BindableProperty.Create("MenuButtonShown", typeof(bool), typeof(CustomNavigationBar), false, propertyChanged: MenuButtonShownChanged);

        public bool MenuButtonShown
        {
            get => (bool)GetValue(MenuButtonShownProperty);
            set => SetValue(MenuButtonShownProperty, value);
        }

        static void MenuButtonShownChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var self = bindable as CustomNavigationBar;
            self.MenuButton.IsVisible = (bool)newValue;
        }

        public event EventHandler MenuButtonClicked;

        private void MenuButton_Clicked(object sender, EventArgs e)
        {
            MenuButtonClicked?.Invoke(this, e);
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
                    OpenSessions.Add(session);
                    session.Register();
	            }

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
                        List<IReadSeekStreamFactory> streamFactoryList;

                        if (!pluginPages.TryGetValue(plugin, out var listPage))
                        {
                            var parameters = new Dictionary<string, (object, string)>
                            {
                                ["Name"] = ("Imported File", "Name of the imported session file")
                            };

                            plugin.GetExtraConfigurationParameters(parameters);
                            var page = new ImportParametersPage(file.Name, parameters);
                            await XamarinHelpers.GetCurrentPage().Navigation.PushAsync(page: page);

                            streamFactoryList = new List<IReadSeekStreamFactory>();
                            pluginPages[plugin] = (streamFactoryList, page);
                        }
                        else
                        {
                            streamFactoryList = listPage.Item1;
                        }

                        streamFactoryList.Add(file);
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
                            var provider = await plugin.Import(file, page.Parameters);
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

        private async void SaveArchiveButton_OnClicked(object sender, EventArgs e)
        {
            var dataPoints = new List<IDataStructure>(DataRegistry.Providers);
            //var sessions = new List<ArchiveSession>(OpenSessions);
            var sessions = new List<ArchiveSession>();

            // TODO: implement
            //var selector = new StorageSelector(sessions, dataPoints);
            
            var sessionName = dataPoints.Any() ? dataPoints.First().Name : "New Session";

            // TODO: this should be run in a thread if we want to control the app while saving:
            await SaveArchive(sessions, dataPoints, sessionName);
        }

        private async void OnSaveComplete(object sender, SaveCompleteArgs args)
        {
            switch (args.Status)
            {
                case SaveStatus.Cancel:
                    return;
                case SaveStatus.Failure:
                    await XamarinHelpers.GetCurrentPage(this).DisplayAlert("Save failed", args.Message, "OK");
                    return;
                case SaveStatus.Success:
                    await XamarinHelpers.GetCurrentPage(this).DisplayAlert("Saving done", "Save completed successfully", "OK");
                    return;
            }
        }

	    public event SaveCompleteEvent SaveComplete;

	    private async Task SaveArchive(ICollection<ArchiveSession> selectedSession,
	        ICollection<IDataStructure> selectedDataPoints, string sessionName)
	    {
            var streamFactory = await _browser.BrowseForSave();
            if (streamFactory == null) return;

            var result = await SaveArchiveProxy(streamFactory, selectedSession, selectedDataPoints, sessionName);
            SaveComplete?.Invoke(this, result);
	    }


        private static async Task<SaveCompleteArgs> SaveArchiveProxy(IReadWriteSeekStreamFactory file, ICollection<ArchiveSession> selectedSession, ICollection<IDataStructure> selectedDataPoints, string sessionName)
	    {
            if ((selectedSession == null || selectedSession.Count == 0) && (selectedDataPoints == null || selectedDataPoints.Count == 0))
            {
                return new SaveCompleteArgs(SaveStatus.Failure, "No data selected for save.");
            }

            var stream = await file.GetReadWriteStream();

	        var archive = Archive.Archive.Create(stream);

	        if (selectedDataPoints?.Count > 0)
	        {
	            var session = ArchiveSession.Create(archive, sessionName);
	            foreach (var dataPoint in selectedDataPoints)
	            {
	                foreach (var child in dataPoint.Children)
	                {
	                    session.AddChild(child);
                    }
                    if (dataPoint is ArchiveSession locArch)
                    {
                        session.AddBasedOnSession(locArch); 
                    }
                }
                archive.AddSession(session);
	        }

	        if (selectedSession != null)
	        {
	            foreach (var session in selectedSession)
	            {
	                archive.AddSession(session);
	            }
	        }

            await archive.WriteFile();
            archive.Close();
	        file.Close();
	        return new SaveCompleteArgs(SaveStatus.Success, "success");
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

    public delegate void SaveCompleteEvent(object sender, SaveCompleteArgs args);

    public class SaveCompleteArgs
    {
        public SaveCompleteArgs(SaveStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public string Message;
        public SaveStatus Status;
    }

    public enum SaveStatus
    {
        Success, Failure, Cancel
    }
}