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

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PlaybarView : ContentView
    {
        public static readonly GridLength DefaultHeight = 40;
        public static readonly GridLength DefaultPreviewHeight = 100;

        private Task _playTask;

        public uint PlayUpdateRate = 40;

        public long WindowSize = 1000000 * 100; // 100s

        private bool _playTaskRunning;

        private long PlayDelayUs {
            get {
                return 1000000L / PlayUpdateRate;
            }
        }
        private int PlayDelayMs
        {
            get
            {
                return (int)(PlayDelayUs / 1000);
            }
        }

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

            _playTask = new Task(() =>
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
                        //(long)(e.NewValue / 10000 * (ViewerContext.AvailableTimeTo - ViewerContext.AvailableTimeFrom)) + ViewerContext.AvailableTimeFrom;

                        if (ViewerContext is TimeSynchronizedContext timeContext)
                        {
                            var offset = (long)(PlayDelayUs * PlaybackSpeed);
                            var newStart = timeContext.SelectedTimeFrom + offset;
                            TimeSlider.Value = TimeToSliderValue(newStart);
                            timeContext.SetSelectedTimeRange(
                                newStart,
                                timeContext.SelectedTimeTo + offset);
                        }
                        //InvalidateLayout();
                    });
                    Thread.Sleep(PlayDelayMs);
                }
            });
            _playTaskRunning = false;
            _playTask.Start();
        }

        public DataViewerContext ViewerContext { get; private set; }
        private readonly TimeSynchronizedContext previewContext = new TimeSynchronizedContext();

        private long SliderValueToTime(double value)
        {
            return (long)(value / 10000 * (ViewerContext.AvailableTimeTo - ViewerContext.AvailableTimeFrom)) + ViewerContext.AvailableTimeFrom;
        }

        private double TimeToSliderValue(long time)
        {
            return 10000 * (time - ViewerContext.AvailableTimeFrom) / (ViewerContext.AvailableTimeTo - ViewerContext.AvailableTimeFrom);
        }

        private void SetSliderTime(long time)
        {
            if (ViewerContext is TimeSynchronizedContext timeContext)
            {
                Debug.WriteLine($"Playbar Slider startpoint: {time}");
                timeContext.SetSelectedTimeRange(time, time + WindowSize);
            }
        }

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (sender == TimeSlider)
            {
                SetSliderTime(SliderValueToTime(e.NewValue));
            }
            // FIXME: Handle the other types of context
        }



        //private double lastFrom = 0;
        //private double? lastTo = 0 ;

        private void ViewerContext_AvailableTimeRangeChanged(DataViewerContext sender, long from, long to)

        {
            Device.BeginInvokeOnMainThread(() =>
            {
                Debug.WriteLine($"Playbar AVAILABLE TIME {from}->{to}");
                LabelTimeFrom.Text = Utils.FormatTime(from);
                LabelTimeTo.Text = Utils.FormatTime(to);
                previewContext.SetSelectedTimeRange(from, to);
                if(ViewerContext.SelectedTimeTo == 0)
                {
                    SetSliderTime(SliderValueToTime(0));
                    InvalidateLayout();
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
            /*if (PreviewDataPoint == null)
            {
                RowDataPreview.Height = DefaultPreviewHeight;
            }
            PreviewDataPoint = datapoint;

            var plot = await LinePlot.Create(datapoint, previewContext);
            ContentGrid.Children.Add(plot, 0, 3, 0, 1);
            if (previewView != null) ContentGrid.Children.Remove(previewView);
            previewView = plot;*/
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
            var trimChars = new char[] { 'x', ' ' };
            PlaybackSpeed = double.Parse(playbackText.TrimEnd(trimChars));
        }
    }
}