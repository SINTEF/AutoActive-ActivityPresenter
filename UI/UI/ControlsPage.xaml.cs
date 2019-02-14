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
        ObservableCollection<string> speeds = new ObservableCollection<string>();


        public ControlsPage ()
		{
            speeds.Add("1/16");
            speeds.Add("1/8");
            speeds.Add("1/4");
            speeds.Add("1/2");
            speeds.Add("1");
            speeds.Add("2");
            speeds.Add("4");
            speeds.Add("8");
            speeds.Add("16");

			InitializeComponent();
            SpeedPicker.ItemsSource = speeds;
            SpeedPicker.SelectedItem = speeds[4];

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