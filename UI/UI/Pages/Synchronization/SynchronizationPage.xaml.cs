using System.Collections.Generic;
using System.Diagnostics;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Helpers;
using SINTEF.AutoActive.UI.Views;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.Synchronization
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SynchronizationPage : ContentPage
    {
        public TimeSynchronizedContext MasterContext = new TimeSynchronizedContext();
        private bool _masterSet;
        public SynchronizationPage()
        {
            InitializeComponent();
            
            TreeView.DataPointTapped += TreeView_DataPointTapped;
            Playbar.ViewerContext = MasterContext;
        }

        public Dictionary<IDataPoint, SynchronizationContext> DataContextDictionary = new Dictionary<IDataPoint, SynchronizationContext>();

        private async void TreeView_DataPointTapped(object sender, IDataPoint e)
        {
            if (!_masterSet)
            {
                var masterLayout = new StackLayout();
                masterLayout.Children.Add(new Label
                {
                    Text = "Master"
                });
                var figure = await FigureView.GetView(e, MasterContext);
                figure.HorizontalOptions = LayoutOptions.FillAndExpand;
                figure.VerticalOptions = LayoutOptions.FillAndExpand;
                masterLayout.Children.Add(figure);

                var frame = new Frame
                {
                    Content = masterLayout,
                    BorderColor = Color.Black,
                    Margin = 5,
                    HorizontalOptions = LayoutOptions.Fill,
                    BackgroundColor = Color.LightSalmon
                };
                Grid.SetColumnSpan(frame, 2);
                Grid.SetColumn(frame, 0);
                Grid.SetRow(frame, 0);
                SyncGrid.Children.Add(frame);

                _masterSet = true;
                return;
            }

            if (!DataContextDictionary.TryGetValue(e, out var context))
            {
                context = new SynchronizationContext(MasterContext);
                //var view = new PlaybarView();
            }

            {
                var figure = await FigureView.GetView(e, context);
                figure.HorizontalOptions = LayoutOptions.FillAndExpand;
                figure.VerticalOptions = LayoutOptions.FillAndExpand;
                
                var layout = new StackLayout();
                layout.Children.Add(figure);
                var slider = new RelativeSlider();
                slider.OffsetChanged += (s, a) => context.Offset = TimeFormatter.TimeFromSeconds(a.NewValue);
                layout.Children.Add(slider);
                var frame = new Frame
                {
                    Content = layout,
                    BorderColor = Color.Black,
                    Margin = 5,
                    HorizontalOptions = LayoutOptions.Fill,
                    BackgroundColor = Color.LightBlue
                };
                Grid.SetColumnSpan(frame, 2);
                Grid.SetColumn(frame, 2);
                Grid.SetRow(frame, 0);
                SyncGrid.Children.Add(frame);
            }
        }
    }
}