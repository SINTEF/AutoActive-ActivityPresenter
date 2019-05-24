using System;
using Android.Content;
using SINTEF.AutoActive.UI.Views;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;


[assembly: ExportRenderer(typeof(DeltaSlider), typeof(SINTEF.AutoActive.UI.Droid.Views.NativeAndroidDeltaSliderRenderer))]
namespace SINTEF.AutoActive.UI.Droid.Views
{
    public class NativeAndroidDeltaSliderRenderer : SliderRenderer
    {
        private DeltaSlider _slider;

        public NativeAndroidDeltaSliderRenderer(Context context) : base(context)
        {
        }

        protected override void OnElementChanged(ElementChangedEventArgs<Slider> e)
        {
            base.OnElementChanged(e);
            if (e.NewElement == null) return;

            _slider = e.NewElement as DeltaSlider;
            if (_slider == null) throw new ArgumentException();

            Control.StartTrackingTouch += (s, a) => _slider.ManipulationStarted();
            Control.StopTrackingTouch += (s, a) => _slider.ManipulationCompleted();

        }
    }
}
