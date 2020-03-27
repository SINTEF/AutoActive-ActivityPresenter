using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.Pages.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SettingsPopupView : Rg.Plugins.Popup.Pages.PopupPage
    {
        private PlaybarView _myPlaybarView;
        private bool _windowSliderInitialised = false;
        public SettingsPopupView()
        {
            InitializeComponent();
            TimelineOverviewSwitch.IsToggled = false;

        }

        public PlaybarView MyPlaybarView
        {
            get { return _myPlaybarView; }
            set { _myPlaybarView = value; }
        }


        public void SetSettings()
        {

            TimelineOverviewSwitch.IsToggled = _myPlaybarView.DataTrackline.IsVisible;
            WindowSlider.Value = _myPlaybarView.WindowSize/ 1000000d;
            PlaybackSpeedButton.Text = _myPlaybarView.PlaybackSpeed.ToString() + "x";

        }

        private void onToggled(object sender, ToggledEventArgs e)
        {
            try
            {

                if (e.Value)
                {
                    _myPlaybarView.DataTrackline.IsVisible = true;    
                }
                else
                {
                    _myPlaybarView.DataTrackline.IsVisible = false;
                }
            }
            catch(NullReferenceException)
            {
                
            }

        }


        private void WindowSlider_OnValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (_windowSliderInitialised)
            {
                _myPlaybarView.WindowSize = (long)(e.NewValue * 1000000);
                _myPlaybarView.SetSliderTime(_myPlaybarView.SliderValueToTime(_myPlaybarView.GetTimeSlider.Value));
            }
            else
            {
                _windowSliderInitialised = true;
            }
        }



        private void PlaybackSpeedButton_Clicked(object sender, EventArgs e)
        {
            if (PlaybackSpeedButton.Text == "1x")
            {
                PlaybackSpeedButton.Text = "2x";
            }
            else if (PlaybackSpeedButton.Text == "2x")
            {
                PlaybackSpeedButton.Text = "5x";
            }
            else if (PlaybackSpeedButton.Text == "5x")
            {
                PlaybackSpeedButton.Text = "0.1x";
            }
            else if (PlaybackSpeedButton.Text == "0.1x")
            {
                PlaybackSpeedButton.Text = "0.25x";
            }
            else if (PlaybackSpeedButton.Text == "0.25x")
            {
                PlaybackSpeedButton.Text = "0.5x";
            }
            else if (PlaybackSpeedButton.Text == "0.5x")
            {
                PlaybackSpeedButton.Text = "1x";
            }
            var trimChars = new[] { 'x', ' ' };
            _myPlaybarView.PlaybackSpeed = double.Parse(PlaybackSpeedButton.Text.TrimEnd(trimChars));
            _myPlaybarView.ViewerContext.PlaybackRate = _myPlaybarView.PlaybackSpeed;
        }




    }
}