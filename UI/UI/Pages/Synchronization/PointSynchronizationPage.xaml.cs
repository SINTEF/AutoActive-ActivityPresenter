using System;
using System.Collections.Generic;
using System.Diagnostics;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.Interfaces;
using SINTEF.AutoActive.UI.Views;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Synchronization
{
    public partial class PointSynchronizationPage : ContentPage, IFigureContainer
    {
        // If start differ by more than this, assume data sets are not synchronized.
        public double OffsetBeforeZeroing = 36000; // 10 hrs [s]

        private readonly TimeSynchronizedContext _masterContext = new TimeSynchronizedContext();
        private bool _masterSet;
        private bool _slaveSet;
        private ITimePoint _masterTime;
        private ITimePoint _slaveTime;
        private RelativeSlider _slaveSlider;
        private SynchronizationContext _slaveContext;

        private long? _selectedMasterTime;
        private long? _selectedSlaveTime;

        public PointSynchronizationPage()
        {
            InitializeComponent();
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
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            TreeView.DataPointTapped -= TreeView_DataPointTapped;
            Playbar.DataTrackline.DeregisterFigureContainer(this);
            _slaveSlider.OffsetChanged -= SlaveSliderOnOffsetChanged;
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
            _selectedMasterTime = 0L;
            MasterTimeButton.Text = "Unset";

            foreach (var figure in GetFigureViewChildren(MasterLayout))
            {
                foreach (var datapoint in figure.DataPoints)
                {
                    DatapointRemoved?.Invoke(this, (datapoint, _masterContext));
                }
                MasterLayout.Children.Clear();
            }
        }

        private void ResetSlave()
        {
            Selected = null;

            _slaveSet = false;
            _slaveTime = null;
            _selectedSlaveTime = 0L;
            SlaveTimeButton.Text = "Unset";

            foreach (var figure in GetFigureViewChildren(SlaveLayout))
            {
                foreach (var datapoint in figure.DataPoints)
                {
                    DatapointRemoved?.Invoke(this, (datapoint, _slaveContext));
                }
                SlaveLayout.Children.Clear();
            }
        }

        private void SlaveTimeButton_OnClicked(object sender, EventArgs e)
        {
            if (_slaveContext == null) return;
            _selectedSlaveTime = _slaveContext.SelectedTimeFrom;
            SlaveTimeButton.Text = TimeFormatter.FormatTime(_selectedSlaveTime.Value, dateSeparator:' ');
        }

        private void MasterTimeButton_OnClicked(object sender, EventArgs e)
        {
            _selectedMasterTime = _masterContext.SelectedTimeFrom;
            MasterTimeButton.Text = TimeFormatter.FormatTime(_selectedMasterTime.Value, dateSeparator: ' ');
        }

        private async void Sync_OnClicked(object sender, EventArgs e)
        {
            if (!_selectedMasterTime.HasValue || !_selectedSlaveTime.HasValue)
            {
                await DisplayAlert("Unset sync time", "A point in both the master time and the slave time must be set.", "OK");
                return;
            }
            _slaveSlider.Offset = TimeFormatter.SecondsFromTime(_selectedSlaveTime.Value - _selectedMasterTime.Value);
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

            DatapointAdded?.Invoke(this, (dataPoint, _masterContext));
        }

        private void SlaveSliderOnOffsetChanged(object sender, ValueChangedEventArgs args)
        {
            _slaveContext.Offset = TimeFormatter.TimeFromSeconds(args.NewValue);
            Playbar.DataTrackline.InvalidateSurface();
        }

        public void InvokeDatapointRemoved(IDataPoint dataPoint, DataViewerContext context)
        {
            DatapointRemoved?.Invoke(this, (dataPoint, context));
        }

        public void InvokeDatapointAdded(IDataPoint dataPoint, DataViewerContext context)
        {
            DatapointAdded?.Invoke(this, (dataPoint, context));
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
                    await DisplayAlert("Illegal datapoint selected", "Can only show data sets with common time", "OK");
                    return;
                }

                if (!isMaster && !_slaveSet)
                {
                    _slaveTime = datapoint.Time;
                    _slaveContext = new SynchronizationContext(_masterContext);
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

                layout.Children.Add(figure);
                DatapointAdded?.Invoke(sender, (datapoint, context));

                if (_slaveSet || isMaster) return;
                _slaveSet = true;

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
                MasterLayout.Children.Remove(figureView);
            }
            else if (figureView.Parent == SlaveLayout)
            {
                SlaveLayout.Children.Remove(figureView);
            }
            else
            {
                Debug.WriteLine("Could not remove frame from layout.");
            }
            
            foreach (var dataPoint in figureView.DataPoints)
            {
                DatapointRemoved?.Invoke(this, (dataPoint, figureView.Context));
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
            _slaveTime.TransformTime(-(_slaveContext.Offset + extraOffset), _slaveContext.Scale);
            _slaveContext.Offset = 0;
        }

        private static long GetOffsetFromTimeStep(TimeStepEvent timeStep)
        {
            long offset;
            switch (timeStep.Length)
            {
                case TimeStepLength.Step:
                    offset = TimeFormatter.TimeFromSeconds(1d / 30);
                    break;
                case TimeStepLength.Short:
                    offset = TimeFormatter.TimeFromSeconds(1);
                    break;
                case TimeStepLength.Large:
                    offset = TimeFormatter.TimeFromSeconds(10);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (timeStep.Direction == TimeStepDirection.Backward)
            {
                offset = -offset;
            }

            return offset;
        }

        private void MasterTimeStepper_OnOnStep(object sender, TimeStepEvent e)
        {
            var context = _masterContext;
            var diff = context.SelectedTimeTo - context.SelectedTimeFrom;

            var offset = GetOffsetFromTimeStep(e);
            var from = context.SelectedTimeFrom + offset;
            var to = from + diff;

            context.SetSelectedTimeRange(from, to);
        }

        private void SlaveTimeStepper_OnOnStep(object sender, TimeStepEvent e)
        {
            _slaveSlider.Offset += TimeFormatter.SecondsFromTime(GetOffsetFromTimeStep(e));
        }

        private void Reset_OnClicked(object sender, EventArgs e)
        {
            Reset();
        }

        private void ResetSlave_OnClicked(object sender, EventArgs e)
        {
            ResetSlave();
        }

        private void SetCommonStart_OnClicked(object sender, EventArgs e)
        {
            SetCommonStartTime(true);
        }
    }
}