using Windows.UI.Xaml.Input;
using SINTEF.AutoActive.UI.Pages;
using SINTEF.AutoActive.UI.UWP.Views;

namespace SINTEF.AutoActive.UI.UWP
{
    public sealed partial class MainPage
    {
        public MainPage()
        {
            this.InitializeComponent();

            LoadApplication(new SINTEF.AutoActive.UI.App());
        }
    }
}
