using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.Pages.Player;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rg.Plugins.Popup.Services;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SettingsPopupView : Rg.Plugins.Popup.Pages.PopupPage
    {
        private bool _windowSliderInitialised = false;
        public SettingsPopupView()
        {
            InitializeComponent();
            TimelineOverviewSwitch.IsToggled = false;

            var versionGetter = DependencyService.Get<SINTEF.AutoActive.UI.Views.IVersionProvider>();

            if (versionGetter != null)
            {
                VersionLabel.Text = versionGetter.Version;
            }
            SliderLabel.Text = WindowSlider.Value.ToString(CultureInfo.InvariantCulture);
        }

        public PlaybarView PlaybarView { get; set; }


        public void SetSettings()
        {

            TimelineOverviewSwitch.IsToggled = PlaybarView.DataTrackline.IsVisible;
            PlaybackSpeedButton.Text = PlaybarView.PlaybackSpeed.ToString() + "x";

            WindowSlider.Maximum = TimeFormatter.SecondsFromTime(Math.Max(
                PlaybarView.ViewerContext.AvailableTimeTo - PlaybarView.ViewerContext.AvailableTimeFrom,
                TimeFormatter.TimeFromSeconds(WindowSlider.Maximum)));
            WindowSlider.Value = TimeFormatter.SecondsFromTime(PlaybarView.WindowSize);
        }

        private void onToggled(object sender, ToggledEventArgs e)
        {

            if (PlaybarView?.DataTrackline == null) return;

            PlaybarView.DataTrackline.IsVisible = e.Value;
        }


        private bool _valueChanging;
        private void WindowSlider_OnValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (PlaybarView == null) return;

            if (_windowSliderInitialised)
            {
                PlaybarView.WindowSize = (long)(e.NewValue * 1000000);
                PlaybarView.SetSliderTime(PlaybarView.SliderValueToTime(PlaybarView.GetTimeSlider.Value));
            }
            else
            {
                _windowSliderInitialised = true;
            }

            SliderLabel.Text = e.NewValue.ToString(CultureInfo.InvariantCulture);
        }

        private void SliderLabel_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_valueChanging) return;
            _valueChanging = true;
            if (double.TryParse(e.NewTextValue, out var result))
            {
                WindowSlider.Value = result;
            }
            _valueChanging = false;
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
            PlaybarView.PlaybackSpeed = double.Parse(PlaybackSpeedButton.Text.TrimEnd(trimChars));
            PlaybarView.ViewerContext.PlaybackRate = PlaybarView.PlaybackSpeed;
        }

        private async void AnnotationsButton_Clicked(object sender, EventArgs e)
        {
            var popupObject = new AnnotationsPopupView();
            await PopupNavigation.Instance.PushAsync(popupObject);
        }
    }
}