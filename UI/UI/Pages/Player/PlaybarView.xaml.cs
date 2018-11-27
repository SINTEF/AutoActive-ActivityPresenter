using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Figures;
using SINTEF.AutoActive.UI.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using SINTEF.AutoActive.UI.Helpers;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlaybarView : ContentView
    {
        public static readonly GridLength DefaultPreviewHeight = 100;
        public DataViewerContext ViewerContext { get; }
        public IDataPoint PreviewDataPoint { get; private set; }

        public double PlaybackSpeed { get; private set; } = 1;
        public uint PlayUpdateRate = 30;
        public long WindowSize = 1000000 * 30; // 30s

        private bool _playTaskRunning;

        private long PlayDelayUs => 1000000L / PlayUpdateRate;
        private int PlayDelayMs => (int)(PlayDelayUs / 1000);

        private long? _lastFrom;
        private long? _lastTo;

        private bool _fromTimeIsCurrent = true;

        private readonly TimeSynchronizedContext _previewContext = new TimeSynchronizedContext();
        private FigureView _previewView;

        public PlaybarView()
        {
            InitializeComponent();
        }

        public PlaybarView (DataViewerContext context)
        {
            InitializeComponent ();

            ViewerContext = context;
            context.AvailableTimeRangeChanged += ViewerContext_AvailableTimeRangeChanged;
            ViewerContext_AvailableTimeRangeChanged(context, context.AvailableTimeFrom, context.AvailableTimeTo);

            var playTask = new Task(PlayButtonLoop);
            _playTaskRunning = false;
            playTask.Start();

            WindowSlider.Value = WindowSize / 1000000d;
        }

        private void PlayButtonLoop()
        {
            while (true)
            {
                Thread.Sleep(PlayDelayMs);
                if (!_playTaskRunning)
                {
                    continue;
                }

                Device.BeginInvokeOnMainThread(() =>
                {
                    if (!(ViewerContext is TimeSynchronizedContext timeContext)) return;

                    var offset = (long) (PlayDelayUs * PlaybackSpeed);
                    var newStart = timeContext.SelectedTimeFrom + offset;
                    TimeSlider.Value = TimeToSliderValue(newStart);

                    timeContext.SetSelectedTimeRange(newStart, timeContext.SelectedTimeTo + offset);
                });
            }
        }

        private long SliderValueToTime(double value)
        {
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

        private void SetSliderTime(long time)
        {
            if (!(ViewerContext is TimeSynchronizedContext timeContext)) return;

            if (_fromTimeIsCurrent)
            {
                LabelTimeFrom.Text = Utils.FormatTime(time);
            }
            timeContext.SetSelectedTimeRange(time, time + WindowSize);
        }

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            // FIXME: Handle the other types of context
            if (sender == TimeSlider)
            {
                SetSliderTime(SliderValueToTime(e.NewValue));
            }
        }

        private void ViewerContext_AvailableTimeRangeChanged(DataViewerContext sender, long from, long to)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                _lastFrom = from;
                _lastTo = to;

                Debug.WriteLine($"Playbar AVAILABLE TIME {from}->{to}");
                if (!_fromTimeIsCurrent)
                {
                    LabelTimeFrom.Text = Utils.FormatTime(from);
                }

                LabelTimeTo.Text = Utils.FormatTime(to);
                _previewContext?.SetSelectedTimeRange(from, to);

                // Check if this is the first time data is added to the screen
                if(ViewerContext.SelectedTimeTo == 0)
                {
                    SetSliderTime(SliderValueToTime(0));
                }
            });
        }

        public async void UseDataPointForTimelinePreview(IDataPoint datapoint)
        {
            if (PreviewDataPoint == null)
            {
                RowDataPreview.Height = DefaultPreviewHeight;
            }
            PreviewDataPoint = datapoint;

            if (_previewView != null)
            {
                ContentGrid.Children.Remove(_previewView);
            }

            _previewView = await LinePlot.Create(datapoint, _previewContext);
            ContentGrid.Children.Add(_previewView, 1, 0);
            _previewContext.SetSelectedTimeRange(_lastFrom, _lastTo);
        }

        private void PlayButton_Clicked(object sender, EventArgs e)
        {
            if (PlayButton.Text == ">")
            {
                _playTaskRunning = true;
                PlayButton.Text = "II";
            }
            else
            {
                _playTaskRunning = false;
                PlayButton.Text = ">";
            }
        }

        private void PlaybackSpeed_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!(PlaybackSpeedPicker?.SelectedItem is string playbackText))
            {
                return;
            }

            var trimChars = new[] {'x', ' '};
            PlaybackSpeed = double.Parse(playbackText.TrimEnd(trimChars));
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
                    Utils.FormatTime(!_fromTimeIsCurrent ? ViewerContext.AvailableTimeFrom : context.SelectedTimeFrom);
            }
        }
    }
}