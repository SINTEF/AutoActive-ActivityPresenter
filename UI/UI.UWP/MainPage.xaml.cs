using Windows.UI.Core.Preview;
using SINTEF.AutoActive.UI.Pages;


namespace SINTEF.AutoActive.UI.UWP
{
    public sealed partial class MainPage
    {
        public MainPage()
        {
            this.InitializeComponent();

            LoadApplication(new SINTEF.AutoActive.UI.App());

            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequested;
        }

        private void ForceClose()
        {
            Windows.UI.Xaml.Application.Current.Exit();
        }

        private void OnCloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            if (!(XamarinHelpers.GetCurrentPage() is SavingPage savingPage))
                return;

            e.Handled = savingPage.ExitShouldBeInterrupted(e.Handled, ForceClose);
        }
    }
}
