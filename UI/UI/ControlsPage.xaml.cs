using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI
{
	public partial class ControlsPage : ContentPage
	{

        public ControlsPage ()
		{
			InitializeComponent();

            PlayBar.Minimum = 0;
            PlayBar.Maximum = 5000;

            PlayBarStartTime.Text = SecondsToTimeString(PlayBar.Minimum);
            PlayBarStopTime.Text = SecondsToTimeString(PlayBar.Maximum);
            AllControls.IsVisible = false;

            var tgr = new TapGestureRecognizer();
            tgr.Tapped += (s, e) => ToggleAllControls();
            PlayBarControls.GestureRecognizers.Add(tgr);
            PlayBar.GestureRecognizers.Add(tgr);
        }

        private void ToggleAllControls()
        {
            AllControls.IsVisible = !AllControls.IsVisible;
        }

        private string SecondsToTimeString(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.ToString(@"hh\:mm\:ss");
        }

        private void OnPlayBarSlide(object sender, EventArgs args)
        {
            PlayBarStartTime.Text = SecondsToTimeString(((Slider)sender).Value);
        }
	}
}