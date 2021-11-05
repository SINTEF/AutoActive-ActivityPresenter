using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.UI.UWP.Views;
using SINTEF.AutoActive.UI.Views.DynamicLayout;
using Xamarin.Forms.Platform.UWP;

[assembly: ExportRenderer(typeof(DraggableButton), typeof(DraggableButtonRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    class DraggableButtonRenderer : ViewRenderer<DraggableButton, Panel>, IDropCollector
    {
        private DraggableButton _draggableButton;
        private Border _frame;
        private Point _elementStartPoint;
        private Point _startPoint;
        private readonly Brush _frameBorderBrush = new SolidColorBrush(Colors.LightGreen);
        private readonly Brush _frameBackgroundBrush = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0));
        //public static bool ListHits;
        private Page _page;
        private Page CurrentPage => _page ?? (_page = GetCurrentPage(this));
        private Panel _mainContainer;
        private Panel MainContainer
        {
            get
            {
                if (_mainContainer != null) return _mainContainer;

                _mainContainer = (Panel)CurrentPage.Content;

                return _mainContainer;
            }
        }

        private static Page GetCurrentPage(FrameworkElement current)
        {
            while (current != null)
            {
                if (current is Page page)
                {
                    return page;
                }

                current = current.Parent as FrameworkElement;
            }

            return null;
        }

        protected override void OnElementChanged(ElementChangedEventArgs<DraggableButton> e)
        {
            base.OnElementChanged(e);

            if (e.NewElement is DraggableButton draggableButton)
                _draggableButton = draggableButton;

            ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
            ManipulationStarted += ControlOnManipulationStarted;
            ManipulationDelta += ControlOnManipulationDelta;
            ManipulationCompleted += ControlOnManipulationCompleted;

        }

        public void ObjectDroppedOn(IDraggable item)
        {

        }

        private const double ElementOffset = 0;
        private void ControlOnManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            e.Handled = true;

            var elWidth = ActualWidth;
            var elHeight = ActualHeight;

            _frame = new Border
            {
                Child = new TextBlock
                {
                    Text = "Test",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                MinWidth = elWidth,
                MinHeight = elHeight,
                BorderBrush = _frameBorderBrush,
                BorderThickness = new Thickness(1),
                Background = _frameBackgroundBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            var pointerPosition = Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerPosition;
            _startPoint = new Point(pointerPosition.X - Window.Current.Bounds.X, pointerPosition.Y - Window.Current.Bounds.Y);
            _elementStartPoint = _startPoint;
            _elementStartPoint.X += ElementOffset;
            _elementStartPoint.Y -= elHeight + ElementOffset;

            SetElementTranslation(0, 0);

            MainContainer.Children.Add(_frame);
        }
        private void ControlOnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            e.Handled = true;
            SetElementTranslation(e.Cumulative.Translation.X, e.Cumulative.Translation.Y);
        }

        private static bool _hasCastError;

        private void SetElementTranslation(double dx, double dy)
        {
            if (_frame == null) return;

            var x = _elementStartPoint.X + dx;
            var y = _elementStartPoint.Y + dy;
            try
            {
                _frame.Translation = new Vector3((float)x, (float)y, 0);
            }
            catch (InvalidCastException)
            {
                if (!_hasCastError)
                {
                    _hasCastError = true;
                    throw;
                }
            }
        }

        private void ControlOnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            e.Handled = true;
            if (_frame == null) return;

            MainContainer.Children.Remove(_frame);
            _frame = null;

            var pos = new Point(_startPoint.X + e.Cumulative.Translation.X, _startPoint.Y + e.Cumulative.Translation.Y);

            var elements = VisualTreeHelper.FindElementsInHostCoordinates(pos, CurrentPage).ToList();
            foreach (var element in elements)
            {
                if (!(element is IDropCollector dropCollector)) continue;

                dropCollector.ObjectDroppedOn(_draggableButton);
                return;
            }
            return;

        }


    }
}
