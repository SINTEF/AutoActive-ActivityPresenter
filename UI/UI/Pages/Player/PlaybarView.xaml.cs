using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Figures;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.UI.Helpers;
using Xamarin.Forms;
using Rg.Plugins.Popup.Services;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.UI.Views;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    public partial class PlaybarView : ContentView, IFigureContainer, ISerializableView
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

        public string GetLabelTimeFrom
        {
            get => LabelTimeFrom.Text;
            set
            {
                LabelTimeFrom.Text = value;
            }
        }

        public Slider GetTimeSlider
        {
            get => TimeSlider;
        }

        public TimeStepper GetTimeStepper
        {
            get => MasterTimeStepper;
        }




        public double PlaybackSpeed { get; set; } = 1;
        public uint PlayUpdateRate = 30;
        public long WindowSize = 1000000 * 30; // 30s
        private DateTime _playStartTime;
        private DateTime _lastTime;
        private bool _playTaskRunning;

        private long PlayDelayUs => 1000000L / PlayUpdateRate;
        private int PlayDelayMs => (int)(PlayDelayUs / 1000);

        public bool FromTimeIsCurrent = true;

        private readonly ManualTimeSynchronizedContext _previewContext = new ManualTimeSynchronizedContext();
        private ManualTimeSynchronizedContext _correlationContext = new ManualTimeSynchronizedContext();
        private LinePlot _previewView;
        private CorrelationPlot _correlationView;

        public void SetAvailableTimeForCorrelationView(long from, long to)
        {

            _correlationContext.SetAvailableTime(from, to);
            _correlationContext?.SetSelectedTimeRange(from, to);

        }

        public void RemoveCorrelationView()
        {
            if (_correlationView == null)
            {
                return;
            }
            _correlationView.RemoveThisView();
            _correlationContext = new ManualTimeSynchronizedContext();
            _correlationView = null;
        }


        public PlaybarView()
        {
            InitializeComponent();

            var playTask = new Task(PlayButtonLoop);
            playTask.Start();

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

                try
                {
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

                        var offset = (long) (offsetDiff * PlaybackSpeed);
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"Caught an exception in the play loop {ex}");
                }
            }
        }

        public long SliderValueToTime(double value)
        {
            if (ViewerContext == null || TimeSlider == null) return 0;
            return (long)(value / TimeSlider.Maximum * (ViewerContext.AvailableTimeTo - ViewerContext.AvailableTimeFrom)) + ViewerContext.AvailableTimeFrom;
        }

        public double TimeToSliderValue(long time)
        {
            var divider = (ViewerContext.AvailableTimeTo - ViewerContext.AvailableTimeFrom);
            if (divider == 0)
            {
                return 0;
            }
            var value = TimeSlider.Maximum * (time - ViewerContext.AvailableTimeFrom);
            return value / divider;
        }

        public void SetSliderTime(long time)
        {
            if (!(ViewerContext is TimeSynchronizedContext timeContext)) return;

            if (FromTimeIsCurrent)
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
                if (!FromTimeIsCurrent)
                {
                    LabelTimeFrom.Text = TimeFormatter.FormatTime(from);
                }

                LabelTimeTo.Text = TimeFormatter.FormatTime(to);
                _availableTime = (from, to);
                _previewContext.SetAvailableTime(from, to);
                _previewContext?.SetSelectedTimeRange(from, to);

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

        public async Task<CorrelationPlot> CorrelationPreview(IDataPoint datapoint, ISyncPage pointSyncPage)
        {
            if (_correlationView != null)
            {
                ContentGrid.Children.Remove(_correlationView);
            }

            // This implements toggling
            if (PreviewDataPoint == datapoint)
            {
                PreviewDataPoint = null;
                return null;
            }

            try
            {
                _correlationView = await CorrelationPlot.Create(datapoint, _correlationContext, pointSyncPage);
            }
            catch (Exception ex)
            {
                await XamarinHelpers.ShowOkMessage("Error", $"Could not create correlation figure:\n{ex}",
                    XamarinHelpers.GetCurrentPage(Navigation));
                return null;
            }

            PreviewDataPoint = datapoint;
            if (PreviewDataPoint == null || _correlationView == null)
            {
                await XamarinHelpers.ShowOkMessage("Error", $"Could not create correlation figure",
                    XamarinHelpers.GetCurrentPage(Navigation));
                return null;
            }

            ContentGrid.Children.Add(_correlationView, 1, 0);
            _correlationView.ContextButtonIsVisible = false;
            return _correlationView;
        }

        public void TimelineExpand_OnClickedExpand_OnClicked()
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

        }

        private void PlayButton_Clicked(bool action)
        {
            if (action)
            {
                _playStartTime = DateTime.Now;
                _playTaskRunning = true;
                _viewerContext.IsPlaying = true;
            }
            else
            {
                _playTaskRunning = false;
                _viewerContext.IsPlaying = false;
            }
        }


        private void LabelTimeFrom_OnClicked(object sender, EventArgs e)
        {
            FromTimeIsCurrent ^= true;
            if (ViewerContext is TimeSynchronizedContext context)
            {
                LabelTimeFrom.Text =
                    TimeFormatter.FormatTime(!FromTimeIsCurrent ? ViewerContext.AvailableTimeFrom : context.SelectedTimeFrom);
            }
        }

        private async void OpenSettings(object sender, EventArgs e)
        {
            var popupObject = new SettingsPopupView();
            popupObject.PlaybarView = this;
            popupObject.SetSettings();
            await PopupNavigation.Instance.PushAsync(popupObject);
        }

        private void TimeStepper_OnOnStep(object sender, TimeStepEvent timeStep)
        {
            switch (timeStep.Play)
            {
                case StartPlay.None:
                    TimeSlider.Value += TimeFormatter.SecondsFromTime(timeStep.AsOffset());
                    break;
                case StartPlay.Start:
                    PlayButton_Clicked(true);
                    break;
                case StartPlay.Stop:
                    PlayButton_Clicked(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public string ViewType => "no.sintef.ui.playbar_view";
        public async Task DeserializeView(JObject root, IDataStructure archive = null)
        {
            await SerializableViewHelper.EnsureViewType(root, this);

            if (root["preview_figure"] is JObject previewFigureObject)
            {
                // If the previewView already exists, reuse it, if not create a new one.
                if (_previewView == null)
                {
                    var view = await FigureView.DeserializeView(previewFigureObject,
                        ViewerContext as TimeSynchronizedContext, archive);
                    UseDataPointForTimelinePreview(view.DataPoints.FirstOrDefault());
                }
                else
                {
                    await _previewView.DeserializeView(previewFigureObject, archive);
                }

            }
        }

        public JObject SerializeView(JObject root = null)
        {
            root = SerializableViewHelper.SerializeDefaults(root, this);
            root["preview_figure"] = _previewView?.SerializeView();

            return root;
        }

        public FigureView Selected { get; set; }
        public void RemoveChild(FigureView figureView)
        {
            if (figureView != _previewView)
            {
                return;
            }
            ContentGrid.Children.Remove(_previewView);
        }

        public event EventHandler<(IDataPoint, DataViewerContext)> DatapointAdded;
        public event EventHandler<(IDataPoint, DataViewerContext)> DatapointRemoved;
        public void InvokeDatapointRemoved(IDataPoint dataPoint, DataViewerContext context)
        {
            if (dataPoint != PreviewDataPoint)
            {
                return;
            }

            PreviewDataPoint = null;
        }

        public void InvokeDatapointAdded(IDataPoint dataPoint, DataViewerContext context)
        {
            throw new NotImplementedException();
        }

        public void KeyDown(object sender, KeyEventArgs args)
        {
            MasterTimeStepper.KeyDown(args);
        }

        public void KeyUp(object sender, KeyEventArgs args)
        {
            MasterTimeStepper.KeyUp(args);
        }
    }
}