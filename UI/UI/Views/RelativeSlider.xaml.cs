using System;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views
{
	public partial class RelativeSlider : ContentView
	{
		public RelativeSlider ()
		{
			InitializeComponent ();
		    Slider.OffsetChanged += (s, e) => OffsetChanged?.Invoke(s, e);
		}

	    public double Offset
	    {
	        get => Slider.Offset;
	        set => Slider.Offset = value;
	    }
	    public event EventHandler<ValueChangedEventArgs> OffsetChanged;
    }
}