using System;
using System.Collections.Generic;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.Views;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Synchronization
{
    public partial class SynchronizationPage : ContentPage
    {
        // If start differ by more than this, assume data sets are not synchronized.
        public double OffsetBeforeZeroing = 36000; // 10 hrs [s]

        private readonly TimeSynchronizedContext _masterContext = new TimeSynchronizedContext();
        private int _index;
        private bool _masterSet;
        private FigureView _masterFigure;
        private ITimePoint _masterTime;

        private readonly Dictionary<ITimePoint, (TimeSynchronizedContext, List<RelativeSlider>)> _dataContextDictionary = new Dictionary<ITimePoint, (TimeSynchronizedContext, List<RelativeSlider>)>();

        public SynchronizationPage()
        {
            InitializeComponent();

            TreeView.DataPointTapped += TreeView_DataPointTapped;
            _masterContext.SetSynchronizedToWorldClock(true);
            Playbar.ViewerContext = _masterContext;
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

        private async void SetMaster(IDataPoint dataPoint)
        {
            var masterLayout = new StackLayout();
            masterLayout.Children.Add(new Label
            {
                Text = "Master"
            });

            _masterFigure = await FigureView.GetView(dataPoint, _masterContext);
            _masterFigure.ContextButtonIsVisible = false;
            _masterFigure.HorizontalOptions = LayoutOptions.FillAndExpand;
            _masterFigure.VerticalOptions = LayoutOptions.FillAndExpand;
            masterLayout.Children.Add(_masterFigure);

            var frame = new Frame
            {
                Content = masterLayout,
                BorderColor = Color.Black,
                Margin = 5,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand,
                BackgroundColor = Color.LightSalmon
            };
            PlaceControl(frame, _index);
            _index++;
            SyncGrid.Children.Add(frame);

            _masterTime = dataPoint.Time;
            _masterSet = true;
            _dataContextDictionary[_masterTime] = (_masterContext, null);

            DataTracklineView.AddDataPoint(dataPoint, _masterContext);
        }

        private async void TreeView_DataPointTapped(object sender, IDataPoint datapoint)
        {
            if (!_masterSet)
            {
                SetMaster(datapoint);
                return;
            }

            if (Selected != null)
            {
                //TODO(sigurdal): Only allow adding of datasets with the same Time? If so: how to get selected time?
            }

            if (!_dataContextDictionary.TryGetValue(datapoint.Time, out var contextSliders))
            {
                contextSliders = (new SynchronizationContext(_masterContext), new List<RelativeSlider>());
                _dataContextDictionary[datapoint.Time] = contextSliders;
            }

            var (context, sliders) = contextSliders;

            if (Selected != null)
            {
                var result = await Selected.ToggleDataPoint(datapoint, context);
                switch (result)
                {
                    case ToggleResult.Added:
                        DataTracklineView.AddDataPoint(datapoint, context);
                        break;
                    case ToggleResult.Removed:
                        DataTracklineView.RemoveDataPoint(datapoint, context);
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

            var layout = new StackLayout();
            layout.Children.Add(figure);
            if (sliders != null && context is SynchronizationContext syncContext)
            {
                var slider = new RelativeSlider();
                sliders.Add(slider);
                slider.OffsetChanged += (s, a) => syncContext.Offset = TimeFormatter.TimeFromSeconds(a.NewValue);
                slider.OffsetChanged += (s, a) => DataTracklineView.InvalidateSurface();
                var offset =
                    TimeFormatter.SecondsFromTime(_masterContext.AvailableTimeFrom - context.AvailableTimeFrom);

                if (Math.Abs(offset) > OffsetBeforeZeroing)
                    slider.Offset = -offset;

                layout.Children.Add(slider);
            }
            else
            {
                layout.Children.Add(new Label {Text = "Master Time", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center});
            }

            var frame = new Frame
            {
                Content = layout,
                BorderColor = Color.Black,
                Margin = 5,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Color.LightBlue
            };

            PlaceControl(frame, _index);
            _index++;
            SyncGrid.Children.Add(frame);
            DataTracklineView.AddDataPoint(datapoint, context);
        }


        public void RemoveChild(FigureView figureView)
        {
            var stackLayout = figureView.Parent;
            if (!(stackLayout.Parent is View frame))
            {
                throw new ArgumentException("A frame is expected as the figure's parent's parent");
            }
            UnPlaceControl(SyncGrid.Children, frame);
            SyncGrid.Children.Remove(frame);
            _index--;
        }

        private static void UnPlaceControl(IList<View> objects, View toRemove)
        {
            // Look for the index of the object to remove. After that move all objects one to the left
            var index = -1;
            for (var i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                if (index != -1)
                {
                    PlaceControl(obj, i - 1);
                }

                if (obj == toRemove)
                {
                    index = i;
                }
            }
        }

        private static void PlaceControl(BindableObject obj, int index)
        {
            if (index < 2)
            {
                Grid.SetColumnSpan(obj, 2);
                Grid.SetColumn(obj, index * 2);
                Grid.SetRow(obj, 0);
                return;
            }

            Grid.SetColumnSpan(obj, 1);
            var modIx = index - 2;
            Grid.SetColumn(obj, modIx % 4);
            Grid.SetRow(obj, modIx / 4 + 1);
        }

        private void Cancel_OnClicked(object sender, EventArgs e)
        {
            Navigation.PopAsync();
        }

        private void Save_OnClicked(object sender, EventArgs e)
        {
            foreach (var syncItem in _dataContextDictionary)
            {
                var (context, _) = syncItem.Value;
                if (context is SynchronizationContext syncContext)
                {
                    syncItem.Key.TransformTime(syncContext.Offset, syncContext.Scale);
                }
            }

            foreach (var syncItem in _dataContextDictionary)
            {
                if (syncItem.Value.Item1 == _masterContext)
                    continue;

                foreach (var item in syncItem.Value.Item2)
                {
                    item.Offset = 0;
                }
            }
        }
    }
}