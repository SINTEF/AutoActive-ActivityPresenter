using SINTEF.AutoActive.UI.Pages.Player;
using SINTEF.AutoActive.UI.UWP.Views;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Xamarin.Forms;
using Xamarin.Forms.Platform.UWP;
using Rectangle = Windows.UI.Xaml.Shapes.Rectangle;

[assembly: ExportRenderer(typeof(PlayerSplitterView),typeof(PlayerSplitterViewRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    public class PlayerSplitterViewRenderer : ViewRenderer<PlayerSplitterView, Rectangle>
    {
        private Rectangle _element;
        public StackOrientation Orientation { get; set; }

        protected override void OnElementChanged(ElementChangedEventArgs<PlayerSplitterView> e)
        {
            base.OnElementChanged(e);

            if (e.OldElement != null)
                e.OldElement.OrientationChanged -= OrientationChanged;
            if (e.NewElement != null)
                e.NewElement.OrientationChanged += OrientationChanged;
            Orientation = e.NewElement?.Orientation ?? StackOrientation.Vertical;

            if (Control != null) return;
            if (_element != null) return;

            _element = new Rectangle
            {
                Fill = new SolidColorBrush(Colors.LightGray),
                Stroke = new SolidColorBrush(Colors.DarkGray),
                StrokeThickness = 1
            };

            _element.PointerPressed += PointerPressed;
            _element.PointerReleased += PointerReleased;
            _element.PointerEntered += PointerEntered;
            _element.PointerExited += PointerExited;

            SetNativeControl(_element);
        }

        private void OrientationChanged(object sender, StackOrientation orientation)
        {
            Orientation = orientation;
        }

        // Set cursor so that we see its draggable
        private static readonly CoreCursor HorizontalDraggableCursor = new CoreCursor(CoreCursorType.SizeWestEast, 0);
        private static readonly CoreCursor VerticalDraggableCursor = new CoreCursor(CoreCursorType.SizeNorthSouth, 0);
        private CoreCursor _previousCursor;

        private new void PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            _previousCursor = Window.Current.CoreWindow.PointerCursor;
            Window.Current.CoreWindow.PointerCursor = Orientation == StackOrientation.Vertical ? VerticalDraggableCursor : HorizontalDraggableCursor;
        }

        private new void PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = _previousCursor;
        }

        // Track pointer movement throughout the window
        private uint? _capturedPointer;
        private PointerPoint _startLocation;

        private new void PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_capturedPointer != null || !_element.CapturePointer(e.Pointer)) return;

            _capturedPointer = e.Pointer.PointerId;
            _startLocation = e.GetCurrentPoint(null);
            _element.PointerMoved += PointerMoved;
            Element?.InvokeDragStart();
        }

        private new void PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _capturedPointer) return;

            _capturedPointer = null;
            _startLocation = null;
            _element.ReleasePointerCapture(e.Pointer);
            _element.PointerMoved -= PointerMoved;
        }

        private new void PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _capturedPointer) return;

            var location = e.GetCurrentPoint(null);
            Element?.InvokeDragged(location.Position.X - _startLocation.Position.X, location.Position.Y - _startLocation.Position.Y);
        }
    }
}
