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

        // The keys must be intercepted here to prevent activation in other views.
        // If handled here, the events on KeypressPageRenderer are not executed.
        protected override void OnPreviewKeyDown(KeyRoutedEventArgs e)
        {
            if (XamarinHelpers.GetCurrentPage() is KeypressPage keyPage)
            {
                var keyArgs = KeypressPageRenderer.VirtualKeyToKeyEvent(e.Key, e.Handled, true);
                KeypressPageRenderer.KeyPageKeyDown(keyPage, keyArgs);
                e.Handled = keyArgs.Handled;
            }
            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewKeyUp(KeyRoutedEventArgs e)
        {
            if (XamarinHelpers.GetCurrentPage() is KeypressPage keyPage)
            {
                var keyArgs = KeypressPageRenderer.VirtualKeyToKeyEvent(e.Key, e.Handled, false);
                KeypressPageRenderer.KeyPageKeyUp(keyPage, keyArgs);
                e.Handled = true;
            }
            base.OnPreviewKeyUp(e);
        }
    }
}
