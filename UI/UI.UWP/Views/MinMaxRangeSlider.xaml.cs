using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using SINTEF.AutoActive.UI.Views;

// Adapted from https://stackoverflow.com/questions/36545896/universal-windows-uwp-range-slider
namespace SINTEF.AutoActive.UI.UWP.Views
{
    public sealed partial class MinMaxRangeSlider : UserControl
    {
        public long Minimum
        {
            get { return (long)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public long Maximum
        {
            get { return (long)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public long SelectedMin
        {
            get { return (long)GetValue(SelectedMinProperty); }
            set { SetValue(SelectedMinProperty, value); }
        }

        public long SelectedMax
        {
            get { return (long)GetValue(SelectedMaxProperty); }
            set { SetValue(SelectedMaxProperty, value); }
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(long), typeof(MinMaxRangeSlider), new PropertyMetadata(0L));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(long), typeof(MinMaxRangeSlider), new PropertyMetadata(1L));

        public static readonly DependencyProperty SelectedMinProperty = DependencyProperty.Register("SelectedMin",
            typeof(long), typeof(MinMaxRangeSlider), new PropertyMetadata(0L, OnSelectedMinPropertyChanged));

        public static readonly DependencyProperty SelectedMaxProperty = DependencyProperty.Register("SelectedMax",
            typeof(long), typeof(MinMaxRangeSlider), new PropertyMetadata(1L, OnSelectedMaxPropertyChanged));

        public MinMaxRangeSlider()
        {
            InitializeComponent();
        }

        public event EventHandler<MinMaxValueChanged> SelectedValueChanged;

        private static void OnSelectedMinPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var slider = (MinMaxRangeSlider) d;
            var newValue = (long) e.NewValue;
            if (e.NewValue != e.OldValue)
            {
                slider.SelectedValueChanged.Invoke(slider, new MinMaxValueChanged(newValue, slider.SelectedMax)
                {
                    MinChanged = true
                });
            }

            if (newValue < slider.Minimum)
            {
                slider.SelectedMin = slider.Minimum;
            }
            else if (newValue > slider.Maximum)
            {
                slider.SelectedMin = slider.Maximum;
            }
            else
            {
                slider.SelectedMin = newValue;
            }

            if (slider.SelectedMin > slider.SelectedMax)
            {
                slider.SelectedMax = slider.SelectedMin;
            }

            slider.UpdateMinThumb(slider.SelectedMin);
        }

        private static void OnSelectedMaxPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var slider = (MinMaxRangeSlider) d;
            var newValue = (long) e.NewValue;
            if (e.NewValue != e.OldValue)
            {
                slider.SelectedValueChanged.Invoke(slider, new MinMaxValueChanged(slider.SelectedMin, newValue)
                {
                    MaxChanged = true
                });
            }

            if (newValue < slider.Minimum)
            {
                slider.SelectedMax = slider.Minimum;
            }
            else if (newValue > slider.Maximum)
            {
                slider.SelectedMax = slider.Maximum;
            }
            else
            {
                slider.SelectedMax = newValue;
            }

            if (slider.SelectedMax < slider.SelectedMin)
            {
                slider.SelectedMin = slider.SelectedMax;
            }

            slider.UpdateMaxThumb(slider.SelectedMax);
        }

        public void UpdateMinThumb(long min, bool update = false)
        {
            if (ContainerCanvas == null) return;

            if (!update && MinThumb.IsDragging) return;

            var relativeLeft = ((double)(min - Minimum) / (Maximum - Minimum)) * ContainerCanvas.ActualWidth;

            Canvas.SetLeft(MinThumb, relativeLeft);
            Canvas.SetLeft(ActiveRectangle, relativeLeft);

            ActiveRectangle.Width = (double)(SelectedMax - min) / (Maximum - Minimum) * ContainerCanvas.ActualWidth;
        }

        public void UpdateMaxThumb(long max, bool update = false)
        {
            if (ContainerCanvas == null) return;
            if (!update && MaxThumb.IsDragging) return;

            var relativeRight = (double)(max - Minimum) / (Maximum - Minimum) * ContainerCanvas.ActualWidth;

            Canvas.SetLeft(MaxThumb, relativeRight);

            ActiveRectangle.Width = (double)(max - SelectedMin) / (Maximum - Minimum) * ContainerCanvas.ActualWidth;
        }

        private void ContainerCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var relativeLeft = ((double)(SelectedMin - Minimum) / (Maximum - Minimum)) * ContainerCanvas.ActualWidth;
            var relativeRight = (double)(SelectedMax - Minimum) / (Maximum - Minimum) * ContainerCanvas.ActualWidth;

            Canvas.SetLeft(MinThumb, relativeLeft);
            Canvas.SetLeft(ActiveRectangle, relativeLeft);
            Canvas.SetLeft(MaxThumb, relativeRight);

            double selectedDiff = SelectedMax - SelectedMin;
            double rangeDiff = Maximum - Minimum;

            if (Math.Abs(selectedDiff) < 0.00001 || Math.Abs(rangeDiff) < 0.00001)
            {
                ActiveRectangle.Width = ContainerCanvas.ActualWidth;
                return;
            }

            ActiveRectangle.Width = selectedDiff / rangeDiff * ContainerCanvas.ActualWidth;
        }

        private void MinThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var min = (long)Math.Round(DragThumb(MinThumb, 0, Canvas.GetLeft(MaxThumb), e.HorizontalChange));
            UpdateMinThumb(min, true);
            SelectedMin = min;
        }

        private void MaxThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var max = (long)Math.Round(DragThumb(MaxThumb, Canvas.GetLeft(MinThumb), ContainerCanvas.ActualWidth, e.HorizontalChange));
            UpdateMaxThumb(max, true);
            SelectedMax = max;
        }

        private double DragThumb(Thumb thumb, double min, double max, double offset)
        {
            var currentPos = Canvas.GetLeft(thumb);
            var nextPos = currentPos + offset;

            nextPos = Math.Max(min, nextPos);
            nextPos = Math.Min(max, nextPos);

            return (Minimum + (nextPos / ContainerCanvas.ActualWidth) * (Maximum - Minimum));
        }

        private void MinThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            UpdateMinThumb(SelectedMin);
            Canvas.SetZIndex(MinThumb, 10);
            Canvas.SetZIndex(MaxThumb, 0);
        }

        private void MaxThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            UpdateMaxThumb(SelectedMax);
            Canvas.SetZIndex(MinThumb, 0);
            Canvas.SetZIndex(MaxThumb, 10);
        }
    }
}