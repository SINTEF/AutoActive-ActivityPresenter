using Windows.UI.Core;
using SINTEF.AutoActive.UI.Pages;
using SINTEF.AutoActive.UI.UWP.Views;
using Xamarin.Forms.Platform.UWP;
using KeyEventArgs = SINTEF.AutoActive.UI.Pages.KeyEventArgs;

[assembly: ExportRenderer(typeof(KeypressPage), typeof(KeypressPageRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    public class KeypressPageRenderer : PageRenderer
    {

        public KeypressPageRenderer()
        {
            Loaded += (sender, e) =>
            {
                var window = CoreWindow.GetForCurrentThread();
                if (window == null) return;
                window.KeyDown += ControlOnKeyDown;
                window.KeyUp += Control_KeyUp;
            };
            Unloaded += (sender, e) =>
            {
                var window = CoreWindow.GetForCurrentThread();
                if (window == null) return;
                window.KeyDown -= ControlOnKeyDown;
                window.KeyUp -= Control_KeyUp;
            };
        }

        private void Control_KeyUp(CoreWindow coreWindow, Windows.UI.Core.KeyEventArgs args)
        {
            (Element as KeypressPage)?.InvokeKeyUp(new KeyEventArgs { Key = args.VirtualKey.ToString() });
        }

        private void ControlOnKeyDown(CoreWindow coreWindow, Windows.UI.Core.KeyEventArgs args)
        {

            (Element as KeypressPage)?.InvokeKeyDown(new KeyEventArgs { Key = args.VirtualKey.ToString()});
        }
    }
}
