using System;
using SINTEF.AutoActive.UI.UWP.Views;
using SINTEF.AutoActive.UI.Views.DynamicLayout;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Xamarin.Forms;
using Xamarin.Forms.Platform.UWP;
using Rectangle = Windows.UI.Xaml.Shapes.Rectangle;

[assembly: ExportRenderer(typeof(DraggableSeparator), typeof(DraggableSeparatorViewRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    public class DraggableSeparatorViewRenderer : ViewRenderer<DraggableSeparator, Rectangle>
    {
        private Rectangle _element;
        public StackOrientation Orientation { get; set; }

        protected override void OnElementChanged(ElementChangedEventArgs<DraggableSeparator> e)
        {
            base.OnElementChanged(e);

            if (e.OldElement != null)
            {
                e.OldElement.OrientationChanged -= OrientationChanged;
                e.OldElement.ViewRemoved -= NewElementOnViewRemoved;

            }

            if (e.NewElement != null)
            {
                e.NewElement.OrientationChanged += OrientationChanged;
                e.NewElement.ViewRemoved += NewElementOnViewRemoved;
            }

            Orientation = e.NewElement?.Orientation ?? StackOrientation.Vertical;

            if (Control != null) return;
            if (_element != null) return;

            _element = new Rectangle{};

            _element.PointerPressed += PointerPressed;
            _element.PointerReleased += PointerReleased;
            _element.PointerEntered += PointerEntered;
            _element.PointerExited += PointerExited;

            SetNativeControl(_element);
        }

        private void NewElementOnViewRemoved(object sender, EventArgs e)
        {
            _element.PointerPressed -= PointerPressed;
            _element.PointerReleased -= PointerReleased;
            _element.PointerEntered -= PointerEntered;
            _element.PointerExited -= PointerExited;

            if (_previousCursor != null)
                Window.Current.CoreWindow.PointerCursor = _previousCursor;
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
            Element?.InvokeDragStart(_startLocation.Position.X, _startLocation.Position.Y);
        }

        private new void PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _capturedPointer) return;

            var startLocation = _startLocation;
            _capturedPointer = null;
            _startLocation = null;
            var location = e.GetCurrentPoint(null);
            _element.ReleasePointerCapture(e.Pointer);
            _element.PointerMoved -= PointerMoved;
            Element?.InvokeDragStop(location.Position.X, location.Position.Y, location.Position.X - startLocation.Position.X, location.Position.Y - startLocation.Position.Y);
        }

        private new void PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _capturedPointer) return;
            if (_startLocation == null) return;

            var location = e.GetCurrentPoint(null);
            Element?.InvokeDragged(location.Position.X, location.Position.Y, location.Position.X - _startLocation.Position.X, location.Position.Y - _startLocation.Position.Y);
        }
    }
}
