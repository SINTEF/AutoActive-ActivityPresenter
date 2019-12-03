using System;
using System.Globalization;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views
{
	public partial class RelativeSlider : ContentView
	{
		public RelativeSlider ()
		{
			InitializeComponent ();
            Slider.OffsetChanged += SliderOnOffsetChanged;
        }

        private void SliderOnOffsetChanged(object sender, ValueChangedEventArgs e)
        {
            OffsetChanged?.Invoke(sender, e);
            if (sender != SliderEntry)
            {
                XamarinHelpers.EnsureMainThread(() => { SliderEntry.Text = e.NewValue.ToString(CultureInfo.InvariantCulture); });
            }
        }

        private void SliderEntryOnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!double.TryParse(e.NewTextValue, out var offset)) return;

            if (Offset != offset)
            {
                Offset = offset;
            }
        }

        public double Offset
        {
            get => Slider?.Offset ?? 0d;
            set
            {
                if (Slider == null) return;
                Slider.Offset = value;
            }
        }

        public event EventHandler<ValueChangedEventArgs> OffsetChanged;
    }
}