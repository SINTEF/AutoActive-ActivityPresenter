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
using SINTEF.AutoActive.UI.Views.TreeView;
using Xamarin.Forms.Platform.UWP;

[assembly: ExportRenderer(typeof(BranchView), typeof(BranchViewRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    internal class BranchViewRenderer : ViewRenderer<BranchView, Panel>, IDropCollector
    {
        private BranchView _branchView;
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

        protected override void OnElementChanged(ElementChangedEventArgs<BranchView> e)
        {
            base.OnElementChanged(e);

            if (e.NewElement is BranchView branchView)
                _branchView = branchView;

            ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
            ManipulationStarted += ControlOnManipulationStarted;
            ManipulationCompleted += ControlOnManipulationCompleted;
            ManipulationDelta += ControlOnManipulationDelta;
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

        private const double ElementOffset = 0;

        private void ControlOnManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            e.Handled = true;
            if (_frame != null)
            {
                MainContainer.Children.Remove(_frame);
                _frame = null;
            }

            var elWidth = ActualWidth;
            var elHeight = ActualHeight;

            _frame = new Border
            {
                Child = new TextBlock
                {
                    Text = _branchView?.Name ?? " - ",
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
                _frame.Translation = new Vector3((float) x, (float) y, 0);
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

                dropCollector.ObjectDroppedOn(_branchView);
                return;
            }

            //if (!ListHits) return;

#if !DEBUG
            return;
#endif
            Debug.WriteLine("Found elements:");
            foreach (var element in elements)
            {
                if (element is IDropCollector dropCollector)
                {
                    dropCollector.ObjectDroppedOn(_branchView);
                }

                if (!(element is FrameworkElement frameworkElement))
                {
                    Debug.WriteLine($"  {element.GetType().Name}");
                    continue;
                }

                if (frameworkElement is Button button)
                {
                    Debug.WriteLine($"  {element.GetType().Name} -> {frameworkElement.Name} - {button.Content}");
                    continue;
                }
                Debug.WriteLine($"  {element.GetType().Name} -> {frameworkElement.Name}");
            }
            Debug.WriteLine("");
        }

        public void ObjectDroppedOn(IDraggable item)
        {
            _branchView.ObjectDroppedOn(item);
        }
    }
}
