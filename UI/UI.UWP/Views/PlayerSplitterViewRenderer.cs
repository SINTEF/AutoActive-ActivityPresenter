using SINTEF.AutoActive.UI.Pages.Player;
using SINTEF.AutoActive.UI.UWP.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Xamarin.Forms.Platform.UWP;

[assembly: ExportRenderer(typeof(PlayerSplitterView),typeof(PlayerSplitterViewRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    public class PlayerSplitterViewRenderer : ViewRenderer<PlayerSplitterView, Rectangle>
    {
        Rectangle element;

        protected override void OnElementChanged(ElementChangedEventArgs<PlayerSplitterView> e)
        {
            base.OnElementChanged(e);

            if (Control == null)
            {
                element = new Rectangle();
                element.Fill = new SolidColorBrush(Colors.White);
                element.PointerPressed += PointerPressed;
                element.PointerReleased += PointerReleased;
                element.PointerEntered += PointerEntered;
                element.PointerExited += PointerExited;
                SetNativeControl(element);
            }
        }

        // Set cursor so that we see its draggable
        static readonly CoreCursor draggableCursor = new CoreCursor(CoreCursorType.SizeWestEast, 0);
        CoreCursor previousCursor;

        private new void PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            previousCursor = Window.Current.CoreWindow.PointerCursor;
            Window.Current.CoreWindow.PointerCursor = draggableCursor;
        }

        private new void PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = previousCursor;
        }

        // Track pointer movement throughout the window
        uint? capturedPointer;
        PointerPoint startLocation;

        private new void PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (capturedPointer == null && element.CapturePointer(e.Pointer))
            {
                capturedPointer = e.Pointer.PointerId;
                startLocation = e.GetCurrentPoint(null);
                element.PointerMoved += PointerMoved;
                Element?.InvokeDragStart();
            }
        }

        private new void PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId == capturedPointer)
            {
                capturedPointer = null;
                startLocation = null;
                element.ReleasePointerCapture(e.Pointer);
                element.PointerMoved -= PointerMoved;
            }
        }

        private new void PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId == capturedPointer)
            {
                var location = e.GetCurrentPoint(null);
                Element?.InvokeDragged(location.Position.X - startLocation.Position.X, location.Position.Y - startLocation.Position.Y);
            }
        }
    }
}
