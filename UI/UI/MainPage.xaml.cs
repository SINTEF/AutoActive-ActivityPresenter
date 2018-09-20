using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using System.Diagnostics;

namespace SINTEF.AutoActive.UI
{
    public partial class MainPage : ContentPage
    {
        int plotCount = 0;

        public MainPage()
        {
            InitializeComponent();

            DataRegistry.DataPointAdded += DataRegistry_DataPointAdded;
        }

        private async void DataRegistry_DataPointAdded(IDataPoint datapoint)
        {
            //var content = Content as StackLayout;

            //if (datapoint is ArchiveVideoVideo)
            //{
            //    var image = new ImageView
            //    {
            //        Data = datapoint,
            //        ViewerContext = MockData.MockData.Context,
            //        HeightRequest = 500,
            //        WidthRequest = 50,
            //    };
            //    /*
            //    // For now, just run it in the backgroun
            //    var viewer = await datapoint.CreateViewerIn(MockData.MockData.Context) as IImageViewer;
            //    await viewer.SetSize(20, 10);

            //    viewer.GetCurrentData();
            //    */
            //    content?.Children.Add(image);
                

            //    return;
            //}

            ////if (plotCount > 3) return;
            //// Show all the graphs
            
            //var plot = new LinePlot
            //{
            //    Data = datapoint,
            //    ViewerContext = MockData.MockData.Context,
            //    HeightRequest = 100,
            //};
            //content?.Children.Add(plot);
            //plotCount++;
        }

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            //MockData.MockData.Context.UpdateRange(e.NewValue, e.NewValue + 100);
        }
    }
}
