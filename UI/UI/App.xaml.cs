using System;
using System.Diagnostics;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Table;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace SINTEF.AutoActive.UI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
#if DEBUG
            LiveReload.Init();
#endif

            MainPage = new NavigationPage(new WelcomePage());
        }

        protected override void OnStart()
        {
            DataRegistry.DataStructureAdded += (DataStructure datastructure) =>
            {
                Debug.WriteLine($"REGISTRY: Datastructure added - {datastructure.Name} - {datastructure.GetType()}");
                if (datastructure is ArchiveTable) { } // FIXME: How do we make sure the dlls are loaded without being used in the UI??
            };
            DataRegistry.DataPointAdded += (IDataPoint datapoint) =>
            {
                Debug.WriteLine($"REGISTRY: Datapoint added - {datapoint.Name} - {datapoint.GetType()}");
            };

            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
