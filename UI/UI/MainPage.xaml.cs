using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

using SINTEF.AutoActive.Databus;

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

        private void DataRegistry_DataPointAdded(IDataPoint datapoint)
        {
            //if (plotCount > 3) return;
            // Show all the graphs
            var content = Content as StackLayout;
            var plot = new LinePlot
            {
                Data = datapoint,
                ViewerContext = MockData.MockData.Context,
                HeightRequest = 100,
            };
            content?.Children.Add(plot);
            plotCount++;
        }

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            MockData.MockData.Context.UpdateRange(e.NewValue, e.NewValue + 100);
        }
    }
}
