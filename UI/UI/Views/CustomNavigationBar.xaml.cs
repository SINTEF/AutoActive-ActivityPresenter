using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SINTEF.AutoActive.Archive.Plugin;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Implementations;
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

        public CustomNavigationBar()
        {
            InitializeComponent();
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
	        var browser = DependencyService.Get<IFileBrowser>();
	        if (browser == null)
	        {
	            await Application.Current.MainPage.DisplayAlert("Open error", "Could get file browser.", "OK");
	            return;
	        }

	        var file = await browser.BrowseForLoad();
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
	            await Application.Current.MainPage.DisplayAlert("Open error", $"Could not open archive:\n{ex.Message}", "OK");
	        }
        }

        private static async void DoImportFiles(IEnumerable<IReadSeekStreamFactory> files)
        {
            IImportPlugin prevPlugin = null;
            Dictionary<string, (object, string)> prevParameters = null;
            foreach (var file in files)
            {
                var ext = file.Extension.ToLower();
                // FIXME: This should probably be handled somewhere else?
                // FIXME: This should also handle a case where multiple importers are possible
                // TODO: Should probably be run on a background thread...

                // Find the proper import plugin to use
                var plugins = PluginService.GetAll<IImportPlugin>(ext);

                var plugin = plugins[0];

                if (prevPlugin == null || plugin != prevPlugin)
                {
                    var parameters = new Dictionary<string, (object, string)>
                    {
                        ["Name"] = ("Imported File", "Name of the imported session file")
                    };

                    plugin.GetExtraConfigurationParameters(parameters);
                    prevParameters = parameters;
                    prevPlugin = plugin;

                    await XamarinHelpers.GetCurrentPage().Navigation.PushAsync(page: new ImportParametersPage(file.Name, prevParameters));
                }

                try
                {
                    var provider = await plugin.Import(file, prevParameters);
                    provider?.Register();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not import file: {ex.Message} \n{ex}");
                    await Application.Current.MainPage.DisplayAlert("Open error",
                        $"Could not import file:\n{ex.Message}", "OK");
                }
            }
        }
        private async void OpenImportButton_OnClicked(object sender, EventArgs e)
        {
            var browser = DependencyService.Get<IFileBrowser>();
            if (browser == null)
            {
                await Application.Current.MainPage.DisplayAlert("Open error", "Could get file browser.", "OK");
                return;
            }

            var files = await browser.BrowseForImportFiles();

            if (files == null) return;

            
            XamarinHelpers.EnsureMainThread(() => DoImportFiles(files));
        }

        private void SaveArchiveButton_OnClicked(object sender, EventArgs e)
        {
            var dataPoints = new List<IDataStructure>(DataRegistry.Providers);
            //var sessions = new List<ArchiveSession>(OpenSessions);
            var sessions = new List<ArchiveSession>();

            // TODO: implement
            //var selector = new StorageSelector(sessions, dataPoints);
            var sessionName = dataPoints.First().Name;


            SaveArchiveButton.Text = "Saving";
            SaveComplete += (s, a) => XamarinHelpers.EnsureMainThread(() => OnSaved(a));

            var saveTask = SaveArchive(sessions, dataPoints, sessionName);

            var thread = new Thread(() => saveTask.Wait());
            thread.Start();

        }

        private void OnSaved(SaveCompleteArgs args)
        {
            if (!args.Success)
            {
                XamarinHelpers.GetCurrentPage(this).DisplayAlert("Save failed", args.Message, "OK");
                SaveArchiveButton.Text = "Save";
                return;
            }
            SaveArchiveButton.Text = "Saved";

	    }

	    public event SaveCompleteEvent SaveComplete;

	    private async Task SaveArchive(ICollection<ArchiveSession> selectedSession,
	        ICollection<IDataStructure> selectedDataPoints, string sessionName)
	    {
	        var result = await SaveArchiveProxy(selectedSession, selectedDataPoints, sessionName);
            SaveComplete?.Invoke(this, result);
	    }


        private async Task<SaveCompleteArgs> SaveArchiveProxy(ICollection<ArchiveSession> selectedSession, ICollection<IDataStructure> selectedDataPoints, string sessionName)
	    {
	        if ((selectedSession == null || selectedSession.Count == 0) && (selectedDataPoints == null || selectedDataPoints.Count == 0))
	        {
	            await XamarinHelpers.GetCurrentPage(Navigation).DisplayAlert("Can not save:", "No data selected.", "OK");
	            return new SaveCompleteArgs(false, "No data selected.");
	        }

	        var browser = DependencyService.Get<IFileBrowser>();
	        if (browser == null)
	        {
	            await XamarinHelpers.GetCurrentPage(Navigation).DisplayAlert("File open error", "Could get file browser.", "OK");
	            return new SaveCompleteArgs(false, "Could not get file browser.");
	        }

	        var file = await browser.BrowseForSave();
	        if (file == null) return new SaveCompleteArgs(false, "No file selected");

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
	        return new SaveCompleteArgs(true, "success");
	    }

	    private void SynchronizationButton_OnClicked(object sender, EventArgs e)
	    {
	        Navigation.PushAsync(new SynchronizationPage());
	    }

        private void Head2Head_OnClicked(object sender, EventArgs e)
        {
            Navigation.PushAsync(new HeadToHead());
        }
    }

    public delegate void SaveCompleteEvent(object sender, SaveCompleteArgs args);

    public class SaveCompleteArgs
    {
        public SaveCompleteArgs(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public string Message;
        public bool Success;

    }
}