﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SINTEF.AutoActive.AutoSync;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure.Columns;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.UI.Views;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Synchronization
{
    public partial class PointSynchronizationPage : KeypressPage, IFigureContainer, ISyncPage
    {
        // If start differ by more than this, assume data sets are not synchronized.
        public double OffsetBeforeZeroing = 36000; // 10 hrs [s]

        private TimeSynchronizedContext _masterContext = new TimeSynchronizedContext();
        private bool _masterSet;
        private bool _slaveSet;
        private ITimePoint _masterTime;
        private ITimePoint _slaveTime;
        private RelativeSlider _slaveSlider;
        private SynchronizationContext _slaveContext;

        private long? _selectedMasterTime;
        private long? SelectedMasterTime
        {
            get => _selectedMasterTime;
            set
            {
                _selectedMasterTime = value;
                MasterTimeButton.Text = _selectedMasterTime.HasValue
                    ? TimeFormatter.FormatTime(_selectedMasterTime.Value, dateSeparator: ' ')
                    : "SET SYNC POINT";
            }
        }

        private long? _selectedSlaveTime;
        private long? SelectedSlaveTime
        {
            get => _selectedSlaveTime;
            set
            {
                _selectedSlaveTime = value;
                SlaveTimeButton.Text = _selectedSlaveTime.HasValue
                    ? TimeFormatter.FormatTime(_selectedSlaveTime.Value, dateSeparator: ' ')
                    : "SET SYNC POINT";
            }
        }

        // The total offset-change in this synchronization operation. This is stored in _lastOffset when saving.
        private long _totalOffset;

        private static long _lastOffset;

        private long _offsetOnFeatureMarkSet;
        private long _totalOffsetOnFeatureMarkSet;

        public PointSynchronizationPage()
        {
            InitializeComponent();
            NavigationBar.SyncPageButton.BackgroundColor = Color.FromHex("23A2B1");
            SlaveTimeStepper.GetPlayButton.IsVisible = false;
            MasterTimeStepper.GetPlayButton.IsVisible = false;
            SlaveTimeStepper.AreButtonsEnabled = false;
            MasterTimeStepper.AreButtonsEnabled = false;

        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            TreeView.DataPointTapped += TreeView_DataPointTapped;
            _masterContext.SetSynchronizedToWorldClock(true);
            _slaveSlider = new RelativeSlider {MinimumHeightRequest = 30};
            _slaveSlider.OffsetChanged += SlaveSliderOnOffsetChanged;
            Playbar.ViewerContext = _masterContext;
            Playbar.DataTrackline.RegisterFigureContainer(this);
            Playbar.GetTimeStepper.AreButtonsVisible = false;
            Playbar.GetTimeStepper.GetPlayButton.IsVisible = true;
            KeyDown += Playbar.KeyDown;
            KeyUp += Playbar.KeyUp;
        }



        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            TreeView.DataPointTapped -= TreeView_DataPointTapped;
            Playbar.DataTrackline.DeregisterFigureContainer(this);
            _slaveSlider.OffsetChanged -= SlaveSliderOnOffsetChanged;
            KeyDown -= Playbar.KeyDown;
            KeyUp -= Playbar.KeyUp;
        }



        private static IEnumerable<FigureView> GetFigureViewChildren(StackLayout masterLayout)
        {
            var list = new List<FigureView>();
            foreach (var child in masterLayout.Children)
            {
                if (child is FigureView figure)
                {
                    list.Add(figure);
                }
            }
            return list;
        }

        private void Reset()
        {
            ResetSlave();

            _masterSet = false;
            _masterTime = null;
            SelectedMasterTime = null;

            foreach (var figure in GetFigureViewChildren(MasterLayout))
            {
                foreach (var datapoint in figure.DataPoints)
                {
                    InvokeDatapointRemoved(datapoint, _masterContext);
                }
                MasterLayout.Children.Clear();
            }

            if (_masterContext != null)
            {
                _masterContext.SyncIsSet = false;
            }
            MasterTimeButton.BackgroundColor = Color.FromRgb(241, 48, 77);
            Playbar.DataTrackline.DeregisterFigureContainer(this);
            _masterContext = new TimeSynchronizedContext();
            _masterContext.SetSynchronizedToWorldClock(true);
            Playbar.ViewerContext = _masterContext;
            Playbar.DataTrackline.RegisterFigureContainer(this);
        }

        private void ResetSlave()
        {
            Selected = null;

            LastOffset.IsEnabled = false;

            _slaveSet = false;
            _slaveTime = null;
            SelectedSlaveTime = null;
            _totalOffset = 0L;
            _lastOffset = 0L;

            foreach (var figure in GetFigureViewChildren(SlaveLayout))
            {
                foreach (var datapoint in figure.DataPoints)
                {
                    InvokeDatapointRemoved(datapoint, _slaveContext);
                }
                SlaveLayout.Children.Clear();
            }

            if (_slaveContext != null)
            {
                _slaveContext.SyncIsSet = false;
            }
            SlaveTimeButton.BackgroundColor = Color.FromRgb(241, 48, 77);
            _slaveSlider.OffsetChanged -= SlaveSliderOnOffsetChanged;
            _slaveSlider = new RelativeSlider();
            _slaveSlider.OffsetChanged += SlaveSliderOnOffsetChanged;
            Playbar.RemoveCorrelationView();
        }

        private void SlaveTimeButton_OnClicked(object sender, EventArgs e)
        {
            if (_slaveContext == null)
            {
                return;
            }
            SelectedSlaveTime = _slaveContext.SelectedTimeFrom;
            _slaveContext.SyncIsSet = true;
            SlaveTimeButton.BackgroundColor = Color.FromRgb(29, 185, 84);
            EnableButtons();
        }

        private void MasterTimeButton_OnClicked(object sender, EventArgs e)
        {
            if (_masterContext == null)
            {
                return;
            }
            SelectedMasterTime = _masterContext.SelectedTimeFrom;
            _masterContext.SyncIsSet = true;
            MasterTimeButton.BackgroundColor = Color.FromRgb(29, 185, 84);
            EnableButtons();
        }

        private List<IDataPoint> GetVisibleDataPoints(StackLayout layout)
        {
            List<IDataPoint> visibleDataPoints = new List<IDataPoint>();

            foreach (var figure in GetFigureViewChildren(layout))
            {
                foreach (var datapoint in figure.DataPoints)
                {
                    visibleDataPoints.Add(datapoint);
                }

            }

            return visibleDataPoints;
        }

        private async void AutoSync_OnClicked(object sender, EventArgs e)
        {
            try
            {
                popupLoadingView.IsVisible = true;
                activityIndicator.IsRunning = true;
                activityIndicator.IsVisible = true;
                List<IDataPoint> visibleMasterDataPoints = GetVisibleDataPoints(MasterLayout);
                List<IDataPoint> visibleSlaveDataPoints = GetVisibleDataPoints(SlaveLayout);
                if (visibleMasterDataPoints.Count != visibleMasterDataPoints.Count)
                {
                    return;
                }

                var (lag, correlation, errorMessage) = await Task.Run(() => CalculateCorrelation(visibleMasterDataPoints, visibleSlaveDataPoints));

                if (errorMessage != null)
                {
                    await DisplayAlert("Warning", errorMessage, "OK");
                    return;
                }
                var time = new TableTimeIndex("time", new Task<long[]>(() => lag), true, "time", "t");
                var correlationColumn = new GenericColumn<float>("correlation", new Task<float[]>(() => correlation), time, "correlation", "cor");
                var correlationPlot = await Playbar.CorrelationPreview(correlationColumn, this);
                correlationPlot.SyncOnMaxValue();
                SelectedSlaveTime = null; // This will likely no longer be valid anyways
            }
            finally
            {
                activityIndicator.IsVisible = false;
                activityIndicator.IsRunning = false;
                popupLoadingView.IsEnabled = false;
                popupLoadingView.IsVisible = false;
            }

        }

        private (long[], float[], string) CalculateCorrelation(List<IDataPoint> visibleMasterDataPoints, List<IDataPoint> visibleSlaveDataPoints)
        {
            SyncByCorrelation sync = new SyncByCorrelation();
            visibleMasterDataPoints.ForEach(x => sync.AddMasterSignal(x));
            visibleSlaveDataPoints.ForEach(x => sync.AddSlaveSignal(x));
            return sync.CorrelateSignals(typeof(PointSynchronizationPage));
        }


        public async void RemoveCorrelationPreview(IDataPoint datapoint)
        {
            await Playbar.CorrelationPreview(datapoint,this);

        }

        private FigureView _selected;
        public FigureView Selected
        {
            get => _selected;
            set
            {
                if (_selected != null) _selected.Selected = false;
                _selected = value;
                if (_selected != null) _selected.Selected = true;
            }
        }

        public event EventHandler<(IDataPoint, DataViewerContext)> DatapointAdded;
        public event EventHandler<(IDataPoint, DataViewerContext)> DatapointRemoved;

        private async void SetMaster(IDataPoint dataPoint)
        {
            var masterFigure = await FigureView.GetView(dataPoint, _masterContext);
            if (masterFigure == null) return;
            masterFigure.ContextButtonIsVisible = true;
            MasterLayout.Children.Add(masterFigure);
            _masterTime = dataPoint.Time;
            _masterSet = true;

            InvokeDatapointAdded(dataPoint, _masterContext);
        }

        public void AdjustOffset(object sender, ValueChangedEventArgs args)
        {
            _slaveSlider.Offset = args.NewValue;
        }

        private void SlaveSliderOnOffsetChanged(object sender, ValueChangedEventArgs args)
        {
            _slaveContext.Offset = TimeFormatter.TimeFromSeconds(args.NewValue);
            if (_masterContext.MarkedFeature != null)
            {
                MarkFeatures_OnClicked(this, new EventArgs());
            }
            else
            {
                Playbar.DataTrackline.InvalidateSurface();
            }
            XamarinHelpers.EnsureMainThread(() => { EnableButtons(); });
        }

        public void InvokeDatapointRemoved(IDataPoint dataPoint, DataViewerContext context)
        {
            DatapointRemoved?.Invoke(this, (dataPoint, context));
            EnableButtons();
        }

        public void InvokeDatapointAdded(IDataPoint dataPoint, DataViewerContext context)
        {
            DatapointAdded?.Invoke(this, (dataPoint, context));
            EnableButtons();
        }

        private async void TreeView_DataPointTapped(object sender, IDataPoint datapoint)
        {
            try
            {
                if (!_masterSet)
                {
                    SetMaster(datapoint);
                    return;
                }

                var isMaster = datapoint.Time == _masterTime;

                if (_slaveSet && !isMaster && datapoint.Time != _slaveTime)
                {
                    await DisplayAlert("Invalid datapoint selected", "Can only show data sets with common time", "OK");
                    return;
                }

                if (!isMaster && !_slaveSet)
                {
                    _slaveTime = datapoint.Time;
                    _slaveContext = new SynchronizationContext(_masterContext);
                    LastOffset.IsEnabled = true;
                    SlaveLayout.Children.Add(_slaveSlider);
                }

                TimeSynchronizedContext context;
                StackLayout layout;
                if (isMaster)
                {
                    context = _masterContext;
                    layout = MasterLayout;
                }
                else
                {
                    context = _slaveContext;
                    layout = SlaveLayout;
                }

                if (Selected != null)
                {
                    await Selected.ToggleDataPoint(datapoint, context);
                    return;
                }

                var figure = await FigureView.GetView(datapoint, context);
                if (figure == null)
                {
                    if (!_slaveSet)
                    {
                        SlaveLayout.Children.Remove(_slaveSlider);
                        _slaveContext = null;
                        _slaveTime = null;
                    }
                    return;
                }

                layout.Children.Insert(0, figure);
                if (_slaveSet || isMaster) return;
                _slaveSet = true;
                InvokeDatapointAdded(datapoint, context);



                SetCommonStartTime(false);
            } catch(Exception ex)
            {
                await XamarinHelpers.ShowOkMessage("Error", $"An error occured:\n{ex.Message}", this);
            }
        }

        private void SetCommonStartTime(bool force)
        {
            var (masterMin, _) = _masterContext.GetAvailableTimeMinMax(true);
            var (slaveMin, _) = _slaveContext.GetAvailableTimeMinMax(true);

            var offset =
                TimeFormatter.SecondsFromTime(masterMin - slaveMin);
            if (force || Math.Abs(offset) > OffsetBeforeZeroing)
                _slaveSlider.Offset = -offset;
        }


        public void RemoveChild(FigureView figureView)
        {
            if (figureView.Parent == MasterLayout)
            {
                int nrOfMasterFigures = MasterLayout.Children.Where(x => x is FigureView).Count();
                if (nrOfMasterFigures == 1)
                {
                    Reset_OnClicked(this, new EventArgs());
                }
                else
                {
                    MasterLayout.Children.Remove(figureView);
                    foreach (var dataPoint in figureView.DataPoints)
                    {
                        InvokeDatapointRemoved(dataPoint, figureView.Context);
                    }
                }

            }
            else if (figureView.Parent == SlaveLayout)
            {
                int nrOfSlaveFigures = SlaveLayout.Children.Where(x => x is FigureView).Count();
                if (nrOfSlaveFigures == 1)
                {
                    ResetSlave_OnClicked(this, new EventArgs());
                }
                else
                {
                    SlaveLayout.Children.Remove(figureView);
                    foreach (var dataPoint in figureView.DataPoints)
                    {
                        InvokeDatapointRemoved(dataPoint, figureView.Context);
                    }
                }

            }
            else
            {
                Debug.WriteLine("Could not remove frame from layout.");
            }

        }

        private void Save_OnClicked(object sender, EventArgs e)
        {
            var extraOffset = 0L;
#if VIDEO_TIME_COMPENSATION
            if (_masterTime is ArchiveVideoTime videoTime)
            {
                //TODO: Check sign of this
                extraOffset = videoTime.VideoPlaybackOffset;
            }
#endif
            var offset = (_selectedMasterTime - _selectedSlaveTime) + extraOffset;
            if (offset.HasValue)
            {
                _totalOffset += offset.Value;
                _lastOffset = offset.Value;
                _slaveTime.TransformTime(offset.Value, _slaveContext.Scale);
                _selectedSlaveTime = null;
            }
            else
            {
                var slaveOffset = _slaveContext.Offset;
                _slaveTime.TransformTime(-slaveOffset, _slaveContext.Scale);
            }

            _slaveSlider.Offset = 0;
            Playbar.RemoveCorrelationView();
            Playbar.DataTrackline.SetCorrelationContext();
        }

        private void MasterTimeStepper_OnOnStep(object sender, TimeStepEvent e)
        {
            var context = _masterContext;
            var diff = context.SelectedTimeTo - context.SelectedTimeFrom;
            var offset = e.AsOffset();
            var from = context.SelectedTimeFrom + offset;
            var to = from + diff;
            var (min, max) = _masterContext.GetAvailableTimeMinMax(true);
            var direction = e.Direction.ToString();

            if ((from < min) && (direction is "Backward"))
            {
                from = min;
                to = from + diff;
            }
            else if((max < to) && (direction is "Forward"))
            {
                from = max;
                to = from + diff;
            }

            context.SetSelectedTimeRange(from, to);
            var value = Playbar.TimeToSliderValue(from);
            Playbar.GetTimeSlider.Value = value;
        }

        private void SlaveTimeStepper_OnOnStep(object sender, TimeStepEvent e)
        {
            _slaveSlider.Offset += TimeFormatter.SecondsFromTime(e.AsOffset());
        }

        private void Reset_OnClicked(object sender, EventArgs e)
        {
            Reset();
        }

        private void ResetSlave_OnClicked(object sender, EventArgs e)
        {
            ResetSlave();
        }

        private async void SetCommonStart_OnClicked(object sender, EventArgs e)
        {
            if (_masterContext == null || _slaveContext == null)
            {
                await XamarinHelpers.ShowOkMessage("Illegal operation", "Select both the master and the slave first.",
                    this);
                return;
            }

            try
            {
                SetCommonStartTime(true);
            }
            catch (Exception ex)
            {
                await XamarinHelpers.ShowOkMessage("Error", $"An error occurred: {ex}", this);
            }
        }

        private void LastSync_OnClicked(object sender, EventArgs e)
        {
            if (_slaveSlider != null)
            {
                _slaveSlider.Offset = TimeFormatter.SecondsFromTime(_lastOffset);
            }
        }

        private void MarkFeatures_OnClicked(object sender, EventArgs e)
        {
            var (masterMin, masterMax) = _masterContext.GetAvailableTimeMinMax(true);
            var (slaveMin, slaveMax) = _slaveContext.GetAvailableTimeMinMax(true);
            slaveMin -= TimeFormatter.TimeFromSeconds(_slaveSlider.Offset);
            slaveMax -= TimeFormatter.TimeFromSeconds(_slaveSlider.Offset);
            double masterFeatureTime;
            double slaveFeatureTime;

            if (sender.GetType() == typeof(Button))
            {
                _offsetOnFeatureMarkSet = TimeFormatter.TimeFromSeconds(_slaveSlider.Offset);
                _totalOffsetOnFeatureMarkSet = _totalOffset;
                var scaleFactor = Playbar.GetTimeSlider.Value / Playbar.GetTimeSlider.Maximum;
                masterFeatureTime = masterMin + ((masterMax - masterMin) * scaleFactor);
                slaveFeatureTime = masterFeatureTime;
            }
            else
            {
                masterFeatureTime = (double)_masterContext.MarkedFeature;
                long slaveSliderOffset = TimeFormatter.TimeFromSeconds(_slaveSlider.Offset);
                long offsetBetweenSliderAndMarker = slaveSliderOffset - _offsetOnFeatureMarkSet;
                long offsetBetweenTotalAndMarker = _totalOffset - _totalOffsetOnFeatureMarkSet;
                slaveFeatureTime = masterFeatureTime - offsetBetweenSliderAndMarker + offsetBetweenTotalAndMarker;
            }


            if (masterMin <= masterFeatureTime && masterFeatureTime <= masterMax)
            {
                _masterContext.MarkedFeature = masterFeatureTime;
            }

            if (slaveMin <= slaveFeatureTime && slaveFeatureTime <= slaveMax)
            {
                _slaveContext.MarkedFeature = slaveFeatureTime;
            }
        }

        protected override bool OnBackButtonPressed()
        {
            base.OnBackButtonPressed();

            return CheckUnsavedSync();
        }

        public bool CheckUnsavedSync()
        {
            if (_slaveContext == null || _slaveContext.Offset == 0L) return false;

            var displayTask = DisplayAlert("Unsaved offset",
                "The offset between master and slave was non-zero, but this has not been saved.\n\nDo you want to save this offset?",
                "Save", "Discard");
            displayTask.ContinueWith(task =>
            {
                if (displayTask.Result)
                {
                    Save_OnClicked(this, new EventArgs());
                }

                XamarinHelpers.EnsureMainThread(async () => await Navigation.PopAsync());
                return false;
            });
            return true;
        }

        private void EnableButtons()
        {
            Playbar.GetTimeStepper.GetPlayButton.IsEnabled = false;
            ResetPage.IsEnabled = false;
            RemoveSlave.IsEnabled = false;
            CommonStart.IsEnabled = false;
            LastOffset.IsEnabled = false;
            SaveSync.IsEnabled = false;
            SlaveTimeStepper.AreButtonsEnabled = false;
            MasterTimeStepper.AreButtonsEnabled = false;
            SlaveTimeButton.IsEnabled = false;
            MasterTimeButton.IsEnabled = false;
            MarkFeature.IsEnabled = false;
            AutoSyncButton.IsEnabled = false;

            if (_slaveSet)
            {
                SlaveTimeStepper.AreButtonsEnabled = true;
                SlaveTimeButton.IsEnabled = true;
            }

            if (_masterSet)
            {
                Playbar.GetTimeStepper.GetPlayButton.IsEnabled = true;
                MasterTimeStepper.AreButtonsEnabled = true;
                MasterTimeButton.IsEnabled = true;
            }

            if (_lastOffset != 0)
            {
                LastOffset.IsEnabled = true;
            }

            if (_masterSet && _slaveSet)
            {
                RemoveSlave.IsEnabled = true;
                CommonStart.IsEnabled = true;
                MarkFeature.IsEnabled = true;
                AutoSyncButton.IsEnabled = true;

                if (_slaveSlider.Offset != 0)
                {
                    SaveSync.IsEnabled = true;
                }

                if (_masterContext.SyncIsSet && _slaveContext.SyncIsSet)
                {
                    SaveSync.IsEnabled = true;
                }
            }

            if (_masterSet || _slaveSet )
            {
                ResetPage.IsEnabled = true;
            }
        }
    }
}