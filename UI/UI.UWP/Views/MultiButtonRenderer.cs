using System.Diagnostics;
using System.Numerics;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using TreeVisualizer;
using Xamarin.Forms.Platform.UWP;
using Windows.Foundation;
using SINTEF.AutoActive.UI.UWP.Views;
using XamarinButton = Xamarin.Forms.Button;
using UwpButton = Windows.UI.Xaml.Controls.Button;
using UwpPage = Windows.UI.Xaml.Controls.Page;

[assembly: ExportRenderer(typeof(MultiButton), typeof(MultiButtonRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    internal class MultiButtonRenderer : ButtonRenderer
    {
        private MultiButton _multiButton;

        protected override void OnElementChanged(ElementChangedEventArgs<XamarinButton> e)
        {
            if (Control != null)
                Control.RightTapped -= ButtonOnRightTapped;

            base.OnElementChanged(e);

            if (e.NewElement is MultiButton multiButton)
                _multiButton = multiButton;

            if (Control == null) return;

            Control.RightTapped += ButtonOnRightTapped;

            //Control.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
            Control.ManipulationStarted += ControlOnManipulationStarted;
            Control.ManipulationCompleted += ControlOnManipulationCompleted;
            Control.ManipulationDelta += ControlOnManipulationDelta;
        }

        private void ControlOnManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            if (_frame != null)
            {
                MainContainer.Children.Remove(_frame);
                _frame = null;
            }

            if (!(sender is UwpButton btn))
            {
                return;
            }

            var elWidth = btn.ActualWidth;
            var elHeight = btn.ActualHeight;

            _frame = new Border
            {
                Child = new TextBlock
                {
                    Text = btn.Content as string ?? " - ",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                MinWidth = elWidth,
                MinHeight = elHeight,
                BorderBrush = _frameBorderBrush,
                Background = _frameBackgroundBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            var pointerPosition = Windows.UI.Core.CoreWindow.GetForCurrentThread().PointerPosition;
            _startPoint = new Point(pointerPosition.X - Window.Current.Bounds.X, pointerPosition.Y - Window.Current.Bounds.Y);
            _startPoint.Y -= elHeight;

            SetElementTranslation(0, 0);

            MainContainer.Children.Add(_frame);
        }

        private void ButtonOnRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            _multiButton?.OnAlternateClicked();
        }


        private UwpPage GetCurrentPage()
        {
            FrameworkElement current = Control;
            while (current != null)
            {
                if (current is UwpPage page)
                {
                    return page;
                }

                current = current.Parent as FrameworkElement;
            }

            return null;
        }

        private Panel _mainContainer;
        private Panel MainContainer
        {
            get
            {
                if (_mainContainer != null) return _mainContainer;

                _mainContainer = (Panel)GetCurrentPage().Content;

                return _mainContainer;
            }
        }

        private UIElement _frame;
        private Point _startPoint;
        private readonly Brush _frameBorderBrush = new SolidColorBrush(Colors.Black);
        private readonly Brush _frameBackgroundBrush = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0));

        private void ControlOnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            SetElementTranslation(e.Cumulative.Translation.X, e.Cumulative.Translation.Y);
        }

        private void SetElementTranslation(double dx, double dy)
        {
            if (_frame == null) return;

            var x = _startPoint.X + dx;
            var y = _startPoint.Y + dy;
            _frame.Translation = new Vector3((float)x, (float)y, 0);
        }


        private void ControlOnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (_frame == null) return;


            MainContainer.Children.Remove(_frame);
            _frame = null;

            //var diffPos = e.Cumulative.Translation;

            //var pos = e.Position;

            var pos = new Point(_startPoint.X + e.Cumulative.Translation.X, _startPoint.Y + e.Cumulative.Translation.Y);

            //Debug.WriteLine(pos);
            //Debug.WriteLine(new Point(_startPoint.X + e.Cumulative.Translation.X, _startPoint.Y + e.Cumulative.Translation.Y));
            //new Point(_startPoint.X +e., _startPoint.Y)

            var elements = VisualTreeHelper.FindElementsInHostCoordinates(pos, GetCurrentPage(), true);
            Debug.WriteLine("Found elements:");
            foreach (var element in elements)
            {
                if (!(element is FrameworkElement frameworkElement))
                {
                    Debug.WriteLine($"{element.GetType().Name}");
                    continue;
                }
                Debug.WriteLine($"{element.GetType().Name}: {frameworkElement.Name}");
            }
            Debug.WriteLine("");
        }
    }
}
