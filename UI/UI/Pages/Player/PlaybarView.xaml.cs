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
        public static readonly GridLength DefaultHeight = 40;
        public static readonly GridLength DefaultPreviewHeight = 100;

        public uint PlayUpdateRate = 30;

        public long WindowSize = 1000000 * 30; // 30s

        private bool _playTaskRunning;

        private long PlayDelayUs => 1000000L / PlayUpdateRate;

        private int PlayDelayMs => (int)(PlayDelayUs / 1000);

        private long? _lastFrom;
        private long? _lastTo;

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

            var playTask = new Task(() =>
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

                        var offset = (long)(PlayDelayUs * PlaybackSpeed);
                        var newStart = timeContext.SelectedTimeFrom + offset;
                        TimeSlider.Value = TimeToSliderValue(newStart);

                        timeContext.SetSelectedTimeRange(
                            newStart,
                            timeContext.SelectedTimeTo + offset);
                    });

                }
            });
            _playTaskRunning = false;
            playTask.Start();
        }

        public DataViewerContext ViewerContext { get; private set; }
        private readonly TimeSynchronizedContext previewContext = new TimeSynchronizedContext();

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

            Debug.WriteLine($"Playbar Slider startpoint: {time}");
            timeContext.SetSelectedTimeRange(time, time + WindowSize);
        }

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (sender == TimeSlider)
            {
                SetSliderTime(SliderValueToTime(e.NewValue));
            }
            // FIXME: Handle the other types of context
        }

        private void ViewerContext_AvailableTimeRangeChanged(DataViewerContext sender, long from, long to)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                _lastFrom = from;
                _lastTo = to;

                Debug.WriteLine($"Playbar AVAILABLE TIME {from}->{to}");
                LabelTimeFrom.Text = Utils.FormatTime(from);
                LabelTimeTo.Text = Utils.FormatTime(to);
                previewContext?.SetSelectedTimeRange(from, to);

                // Check if this is the first time data is added to the screen
                if(ViewerContext.SelectedTimeTo == 0)
                {
                    SetSliderTime(SliderValueToTime(0));
                }

                //if (lastTo < to)
                //{
                //    TimeSlider.Value = to - 100;
                //}
                //lastFrom = from;
                //lastTo = to;

            });
        }

        /* --- Public API --- */
        public IDataPoint PreviewDataPoint { get; private set; }
        public double PlaybackSpeed { get; private set; } = 1;

        private FigureView previewView;

        public async void UseDataPointForTimelinePreview(IDataPoint datapoint)
        {
            if (PreviewDataPoint == null)
            {
                RowDataPreview.Height = DefaultPreviewHeight;
            }
            PreviewDataPoint = datapoint;

            if (previewView != null)
            {
                ContentGrid.Children.Remove(previewView);
            }

            previewView = await LinePlot.Create(datapoint, previewContext);
            ContentGrid.Children.Add(previewView, 1, 0);
            previewContext.SetSelectedTimeRange(_lastFrom, _lastTo);
        }

        private void PlayButton_Clicked(object sender, EventArgs e)
        {
            if (PlayButton.Text == ">")
            {
                _playTaskRunning = true;
                PlayButton.Text = "II";
            } else
            {
                _playTaskRunning = false;
                PlayButton.Text = ">";
            }
        }

        private void PlaybackSpeed_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (PlaybackSpeedPicker == null)
                return;
            var playbackText = PlaybackSpeedPicker.SelectedItem as string;
            var trimChars = new[] { 'x', ' ' };
            PlaybackSpeed = double.Parse(playbackText.TrimEnd(trimChars));
        }
    }
}