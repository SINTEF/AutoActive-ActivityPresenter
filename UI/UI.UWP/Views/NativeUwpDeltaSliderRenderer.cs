using System;
using Windows.UI.Xaml.Input;
using SINTEF.AutoActive.UI.Views;
using Xamarin.Forms;
using Xamarin.Forms.Platform.UWP;


[assembly: ExportRenderer(typeof(DeltaSlider), typeof(SINTEF.AutoActive.UI.UWP.Views.NativeUwpDeltaSliderRenderer))]
namespace SINTEF.AutoActive.UI.UWP.Views
{
    public class NativeUwpDeltaSliderRenderer : SliderRenderer
    {
        private DeltaSlider _slider;
        protected override void OnElementChanged(ElementChangedEventArgs<Slider> e)
        {
            base.OnElementChanged(e);
            _slider = e.NewElement as DeltaSlider;
            if (_slider == null) throw new ArgumentException();

            Control.ManipulationStarted += (s, a) => _slider.ManipulationStarted();
            Control.ManipulationCompleted += (s, a) => _slider.ManipulationCompleted();
            Control.ManipulationMode = ManipulationModes.All;
        }
    }
}
