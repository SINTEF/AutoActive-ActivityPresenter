using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private ITimePoint _masterTime;

        private readonly Dictionary<ITimePoint, Tuple<SynchronizationContext, List<RelativeSlider>>> _dataContextDictionary = new Dictionary<ITimePoint, Tuple<SynchronizationContext, List<RelativeSlider>>>();

        public SynchronizationPage()
        {
            InitializeComponent();

            TreeView.DataPointTapped += TreeView_DataPointTapped;
            _masterContext.SetSynchronizedToWorldClock(true);
            Playbar.ViewerContext = _masterContext;
        }

        private async void SetMaster(IDataPoint dataPoint)
        {
            var masterLayout = new StackLayout();
            masterLayout.Children.Add(new Label
            {
                Text = "Master"
            });
            var figure = await FigureView.GetView(dataPoint, _masterContext);
            figure.HorizontalOptions = LayoutOptions.FillAndExpand;
            figure.VerticalOptions = LayoutOptions.FillAndExpand;
            masterLayout.Children.Add(figure);

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
        }

        private async void TreeView_DataPointTapped(object sender, IDataPoint e)
        {
            if (!_masterSet)
            {
                SetMaster(e);
                return;
            }

            if (e.Time == _masterTime)
            {
                await DisplayAlert("Data error", "Can't synchronize with the master context itself", "OK");
                return;
            }

            if (!_dataContextDictionary.TryGetValue(e.Time, out var context))
            {
                context = new Tuple<SynchronizationContext, List<RelativeSlider>>(new SynchronizationContext(_masterContext), new List<RelativeSlider>());
                _dataContextDictionary[e.Time] = context;
            }

            var figure = await FigureView.GetView(e, context.Item1);
            figure.HorizontalOptions = LayoutOptions.FillAndExpand;
            figure.VerticalOptions = LayoutOptions.FillAndExpand;

            var layout = new StackLayout();
            layout.Children.Add(figure);
            var slider = new RelativeSlider();
            context.Item2.Add(slider);
            slider.OffsetChanged += (s, a) => context.Item1.Offset = TimeFormatter.TimeFromSeconds(a.NewValue);
            var offset = TimeFormatter.SecondsFromTime(_masterContext.AvailableTimeFrom - context.Item1.AvailableTimeFrom);

            if (Math.Abs(offset) > OffsetBeforeZeroing)
                slider.Offset = -offset;

            layout.Children.Add(slider);
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
                Debug.WriteLine($"Offset: {syncItem.Value.Item1.Offset}");
                syncItem.Key.TransformTime(-syncItem.Value.Item1.Offset, syncItem.Value.Item1.Scale);
            }

            foreach (var syncItem in _dataContextDictionary)
            {
                foreach (var item in syncItem.Value.Item2)
                {
                    item.Offset = 0;
                }
            }
        }
    }
}