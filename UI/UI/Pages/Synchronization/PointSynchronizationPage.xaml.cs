﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private FigureView _masterFigure;
        private ITimePoint _masterTime;
        private ITimePoint _slaveTime;
        private RelativeSlider _slaveSlider;
        private SynchronizationContext _slaveContext;

        public PointSynchronizationPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            TreeView.DataPointTapped += TreeView_DataPointTapped;
            _masterContext.SetSynchronizedToWorldClock(true);
            Playbar.ViewerContext = _masterContext;
            Playbar.DataTrackline.RegisterFigureContainer(this);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            TreeView.DataPointTapped -= TreeView_DataPointTapped;
            Playbar.DataTrackline.DeregisterFigureContainer(this);
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
            _masterFigure = await FigureView.GetView(dataPoint, _masterContext);
            _masterFigure.ContextButtonIsVisible = true;
            _masterFigure.HorizontalOptions = LayoutOptions.FillAndExpand;
            _masterFigure.VerticalOptions = LayoutOptions.FillAndExpand;
            MasterLayout.Children.Add(_masterFigure);

            _masterTime = dataPoint.Time;
            _masterSet = true;

            DatapointAdded?.Invoke(this, (dataPoint, _masterContext));
        }

        public void InvokeDatapointRemoved(IDataPoint dataPoint, DataViewerContext context)
        {
            DatapointRemoved?.Invoke(this, (dataPoint, context));
        }
        private async void TreeView_DataPointTapped(object sender, IDataPoint datapoint)
        {
            if (!_masterSet)
            {
                SetMaster(datapoint);
                return;
            }

            if (!_slaveSet)
            {
                _slaveTime = datapoint.Time;
                _slaveContext = new SynchronizationContext(_masterContext);
                _slaveSlider = new RelativeSlider();
                _slaveSlider.OffsetChanged += (s, a) => _slaveContext.Offset = TimeFormatter.TimeFromSeconds(a.NewValue);
                _slaveSlider.OffsetChanged += (s, a) => Playbar.DataTrackline.InvalidateSurface();
                _slaveSet = true;
                var offset =
                    TimeFormatter.SecondsFromTime(_masterContext.AvailableTimeFrom - _slaveContext.AvailableTimeFrom);
                if (Math.Abs(offset) > OffsetBeforeZeroing)
                    _slaveSlider.Offset = -offset;
                SlaveLayout.Children.Add(_slaveSlider);
            }

            if (Selected != null)
            {
                //TODO(sigurdal): Only allow adding of datasets with the same Time? If so: how to get selected time?
            }

            if (datapoint.Time != _masterTime && datapoint.Time != _slaveTime)
            {
                await DisplayAlert("Illegal time selected", "Can only show data sets with common time", "OK");
                return;
            }

            TimeSynchronizedContext context = datapoint.Time == _masterTime ? _masterContext : _slaveContext;
            StackLayout layout;
            if (datapoint.Time == _masterTime)
            {
                context = _masterContext;
                layout = MasterLayout;
            } else
            {
                context = _slaveContext;
                layout = SlaveLayout;
            }

            if (Selected != null)
            {
                var result = await Selected.ToggleDataPoint(datapoint, context);
                switch (result)
                {
                    case ToggleResult.Added:
                        DatapointAdded?.Invoke(sender, (datapoint, context));
                        break;
                    case ToggleResult.Removed:
                        DatapointRemoved?.Invoke(sender, (datapoint, context)); ;
                        break;
                    case ToggleResult.Cancelled:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                return;
            }

            var figure = await FigureView.GetView(datapoint, context);
            figure.HorizontalOptions = LayoutOptions.FillAndExpand;
            figure.VerticalOptions = LayoutOptions.FillAndExpand;

            layout.Children.Add(figure);
            DatapointAdded?.Invoke(sender, (datapoint, context));
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

        private void Cancel_OnClicked(object sender, EventArgs e)
        {
            Navigation.PopAsync();
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
    }
}