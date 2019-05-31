using System;
using System.Diagnostics;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Table;
using SINTEF.AutoActive.UI.Pages.Player;
using SINTEF.AutoActive.UI.Pages;

//[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace SINTEF.AutoActive.UI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            // Load the plugins

            //MainPage = new NavigationPage(new WelcomePage());
            MainPage = new CustomNavigationPage(new PlayerPage());
            //MainPage = new PlayerPage();
        }

        protected override void OnStart()
        {
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
