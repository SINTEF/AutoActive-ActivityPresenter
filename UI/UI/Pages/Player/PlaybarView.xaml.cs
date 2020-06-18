using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Figures;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.Views;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    public partial class PlaybarView : ContentView
    {
        public static readonly GridLength DefaultPreviewHeight = 100;
        public static readonly GridLength DefaultTimelineHeight = 100;

        private SingleSetDataViewerContext _viewerContext;
        public SingleSetDataViewerContext ViewerContext
        {
            get => _viewerContext;
            set
            {
                if (_viewerContext != null)
                {
                    _viewerContext.AvailableTimeRangeChanged -= ViewerContext_AvailableTimeRangeChanged;
                }

                _viewerContext = value;

                if (_viewerContext == null) return;

                _viewerContext.AvailableTimeRangeChanged += ViewerContext_AvailableTimeRangeChanged;
                ViewerContext_AvailableTimeRangeChanged(_viewerContext, _viewerContext.AvailableTimeFrom, _viewerContext.AvailableTimeTo);
            }
        }

        private IDataPoint _previewDataPoint;

        public IDataPoint PreviewDataPoint
        {
            get => _previewDataPoint;
            private set
            {
                _previewDataPoint = value;

                RowDataPreview.Height = _previewDataPoint != null ? DefaultPreviewHeight : 0;
            }
        }

        public double PlaybackSpeed { get; private set; } = 1;
        public uint PlayUpdateRate = 30;
        public long WindowSize = 1000000 * 30; // 30s
        private DateTime _playStartTime;
        private DateTime _lastTime;
        private bool _playTaskRunning;

        private long PlayDelayUs => 1000000L / PlayUpdateRate;
        private int PlayDelayMs => (int)(PlayDelayUs / 1000);

        private bool _fromTimeIsCurrent = true;

        private readonly ManualTimeSynchronizedContext _previewContext = new ManualTimeSynchronizedContext();
        private LinePlot _previewView;

        public PlaybarView()
        {
            InitializeComponent();

            var playTask = new Task(PlayButtonLoop);
            playTask.Start();

            WindowSlider.Value = WindowSize / 1000000d;

            DataTrackline.Playbar = this;
        }

        private bool _playSliderChanging;

        private void PlayButtonLoop()
        {
            _playTaskRunning = false;
            _playStartTime = DateTime.Now;
            _lastTime = DateTime.Now;

            while (true)
            {
                Thread.Sleep(PlayDelayMs);
                if (!_playTaskRunning)
                {
                    _lastTime = DateTime.Now;
                    continue;
                }

                Device.BeginInvokeOnMainThread(() =>
                {
                    if (!(ViewerContext is TimeSynchronizedContext timeContext)) return;
                    var now = DateTime.Now;
                    //var offset = (long) (PlayDelayUs * PlaybackSpeed);
                    var offsetDiff = TimeFormatter.TimeFromTimeSpan(now - _lastTime);

                    if (offsetDiff > 2 * PlayDelayUs)
                    {
                        offsetDiff = PlayDelayUs;
                    }

                    var offset = (long)(offsetDiff * PlaybackSpeed);
                    var newStart = timeContext.SelectedTimeFrom + offset;

                    if (newStart > timeContext.AvailableTimeTo)
                    {
                        newStart = timeContext.AvailableTimeTo;
                    }

                    _playSliderChanging = true;
                    TimeSlider.Value = TimeToSliderValue(newStart);
                    _playSliderChanging = false;

                    SetSliderTime(newStart);
                    _lastTime = now;
                });
            }
        }

        private long SliderValueToTime(double value)
        {
            if (ViewerContext == null || TimeSlider == null) return 0;
            return (long)(value / TimeSlider.Maximum * (ViewerContext.AvailableTimeTo - ViewerContext.AvailableTimeFrom)) + ViewerContext.AvailableTimeFrom;
        }

        private double TimeToSliderValue(long time)
        {
            var divider = (ViewerContext.AvailableTimeTo - ViewerContext.AvailableTimeFrom);
            if (divider == 0)
            {
                return 0;
            }
            var value = TimeSlider.Maximum * (time - ViewerContext.AvailableTimeFrom) ;
            return  value / divider;
        }

        public void SetSliderTime(long time)
        {
            if (!(ViewerContext is TimeSynchronizedContext timeContext)) return;

            if (_fromTimeIsCurrent)
            {
                LabelTimeFrom.Text = TimeFormatter.FormatTime(time);
            }
            timeContext.SetSelectedTimeRange(time, time + WindowSize);
        }

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (_playSliderChanging) return;

            SetSliderTime(SliderValueToTime(e.NewValue));
        }

        private (long, long) _availableTime;

        private void ViewerContext_AvailableTimeRangeChanged(DataViewerContext sender, long from, long to)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine($"Playbar AVAILABLE TIME {from}->{to}");
                if (!_fromTimeIsCurrent)
                {
                    LabelTimeFrom.Text = TimeFormatter.FormatTime(from);
                }

                LabelTimeTo.Text = TimeFormatter.FormatTime(to);
                _availableTime = (from, to);
                _previewContext.SetAvailableTime(from, to);
                _previewContext?.SetSelectedTimeRange(from, to);
                RangeSlider.SetAvailableTime(from, to);

                // Ensure that the current selected time is in a valid range.
                if (ViewerContext.SelectedTimeFrom < ViewerContext.AvailableTimeFrom)
                {
                    SetSliderTime(ViewerContext.AvailableTimeFrom);
                }
                else if (ViewerContext.SelectedTimeFrom > ViewerContext.AvailableTimeTo)
                {
                    var newTime = ViewerContext.AvailableTimeTo - WindowSize;
                    SetSliderTime(newTime < ViewerContext.AvailableTimeFrom ? ViewerContext.AvailableTimeFrom : newTime);
                }
                else
                {
                    SetSliderTime(ViewerContext.SelectedTimeFrom);
                }
            });
        }

        public async void UseDataPointForTimelinePreview(IDataPoint datapoint)
        {
            if (_previewView != null)
            {
                ContentGrid.Children.Remove(_previewView);
            }

            // This implements toggling
            if (PreviewDataPoint == datapoint)
            {
                PreviewDataPoint = null;
                return;
            }

            if (!(datapoint is TableColumn))
            {
                await XamarinHelpers.ShowOkMessage("Error", "Only lines are supported for timeline view.",
                    XamarinHelpers.GetCurrentPage(Navigation));
                return;
            }

            try
            {
                _previewView = await LinePlot.Create(datapoint, _previewContext);
            }
            catch (Exception ex)
            {
                await XamarinHelpers.ShowOkMessage("Error", $"Could not create timeline preview:\n{ex}",
                    XamarinHelpers.GetCurrentPage(Navigation));
                return;
            }

            PreviewDataPoint = datapoint;
            if (PreviewDataPoint == null)
            {
                return;
            }

            if (_previewView == null)
            {
                await XamarinHelpers.ShowOkMessage("Error", $"Could not create timeline preview",
                    XamarinHelpers.GetCurrentPage(Navigation));
                return;
            }
            _previewView.ContextButtonIsVisible = false;
            _previewView.AxisValuesVisible = false;
            _previewView.CurrentTimeVisible = false;

            ContentGrid.Children.Add(_previewView, 1, 0);
            _previewContext.SetAvailableTime(_availableTime.Item1, _availableTime.Item2);
            _previewContext.SetSelectedTimeRange(_availableTime.Item1, _availableTime.Item2);
        }

        private void TimelineExpand_OnClickedExpand_OnClicked(object sender, EventArgs e)
        {
            var wasVisible = RowTimelineView.Height.Value == 0d;
            if (wasVisible)
            {
                RowTimelineView.Height = DefaultTimelineHeight;
                DataTrackline.IsVisible = true;
            }
            else
            {
                RowTimelineView.Height = 0;
                DataTrackline.IsVisible = false;
            }

            TimelineExpand.Text = wasVisible ? "v" : "^";
        }

        private void PlayButton_Clicked(object sender, EventArgs e)
        {
            if (PlayButton.Text == ">")
            {
                _playStartTime = DateTime.Now;
                _playTaskRunning = true;
                PlayButton.Text = "II";
                _viewerContext.IsPlaying = true;
            }
            else
            {
                _playTaskRunning = false;
                PlayButton.Text = ">";
                _viewerContext.IsPlaying = false;
            }
        }
        private void PlaybackSpeedButton_Clicked(object sender, EventArgs e)
        {
            if (PlaybackSpeedButton.Text == "1x")
            {
                PlaybackSpeedButton.Text = "2x";
            }
            else if(PlaybackSpeedButton.Text == "2x")
            {
                PlaybackSpeedButton.Text = "5x";
            }
            else if(PlaybackSpeedButton.Text == "5x")
            {
                PlaybackSpeedButton.Text = "0.1x";
            }
            else if(PlaybackSpeedButton.Text == "0.1x")
            {
                PlaybackSpeedButton.Text = "0.25x";
            }
            else if(PlaybackSpeedButton.Text == "0.25x")
            {
                PlaybackSpeedButton.Text = "0.5x";
            }
            else if(PlaybackSpeedButton.Text == "0.5x")
            {
                PlaybackSpeedButton.Text = "1x";
            }
            var trimChars = new[] { 'x', ' ' };
            PlaybackSpeed = double.Parse(PlaybackSpeedButton.Text.TrimEnd(trimChars));
            _viewerContext.PlaybackRate = PlaybackSpeed;
        }

        private void ButtonUpExpand_OnClicked(object sender, EventArgs e)
        {
            WindowSliderSelector.IsVisible ^= true;
            ButtonUpExpand.Text = WindowSliderSelector.IsVisible ? "v" : "^";
            WindowSlider.Value = WindowSize / 1000000d;
        }

        private void WindowSlider_OnValueChanged(object sender, ValueChangedEventArgs e)
        {
            WindowLengthLabel.Text = $"{e.NewValue} s";
            WindowSize = (long)(e.NewValue * 1000000);
            SetSliderTime(SliderValueToTime(TimeSlider.Value));
        }

        private void LabelTimeFrom_OnClicked(object sender, EventArgs e)
        {
            _fromTimeIsCurrent ^= true;
            if (ViewerContext is TimeSynchronizedContext context)
            {
                LabelTimeFrom.Text =
                    TimeFormatter.FormatTime(!_fromTimeIsCurrent ? ViewerContext.AvailableTimeFrom : context.SelectedTimeFrom);
            }
        }

        private void RangeSlider_OnSelectedValueChanged(object sender, MinMaxValueChanged e)
        {
            //throw new NotImplementedException($"Range slider changed: {e.Min} {e.Max}");
            //TODO(sigurdal): Implement this
        }
    }
}