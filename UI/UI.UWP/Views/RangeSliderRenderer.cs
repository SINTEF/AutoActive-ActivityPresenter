using SINTEF.AutoActive.UI.UWP.Views;
using SINTEF.AutoActive.UI.Views;
using Xamarin.Forms.Platform.UWP;

[assembly: ExportRenderer(typeof(RangeSlider), typeof(RangeSliderRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    internal class RangeSliderRenderer : ViewRenderer<RangeSlider, MinMaxRangeSlider>
    {
        private RangeSlider _rangeSlider;
        private MinMaxRangeSlider _nativeSlider;
        protected override void OnElementChanged(ElementChangedEventArgs<RangeSlider> e)
        {
            base.OnElementChanged(e);

            if (_nativeSlider != null)
            {
                _nativeSlider.SelectedValueChanged -= SelectedValueChanged;
            }

            if (_rangeSlider != null)
            {
                _rangeSlider.AvailableTimeChanged -= RangeSliderOnAvailableTimeChanged;
            }
            e.OldElement?.Dispose();

            if (e.NewElement == null) return;
            _rangeSlider = e.NewElement;

            _rangeSlider.AvailableTimeChanged += RangeSliderOnAvailableTimeChanged;

            _nativeSlider = new MinMaxRangeSlider();
            _nativeSlider.SelectedValueChanged += SelectedValueChanged;
            SetNativeControl(_nativeSlider);
        }

        private void RangeSliderOnAvailableTimeChanged(object sender, MinMaxValueChanged e)
        {
            _nativeSlider.Maximum = e.Max;
            _nativeSlider.Minimum = e.Min;
            _nativeSlider.SelectedMin = e.Min;
            _nativeSlider.SelectedMax = e.Max;
        }

        private void SelectedValueChanged(object sender, MinMaxValueChanged e)
        {
            _rangeSlider.OnSelectedValueChanged(e);
        }
    }
}
