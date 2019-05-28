using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.UI.Figures;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.HeadToHead
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class HeadToHead : ContentPage
    {
        public HeadToHead()
        {
            InitializeComponent();
            TreeView.DataPointTapped += TreeViewOnDataPointTapped;
            OffsetSlider.OffsetChanged += OffsetSliderOnOffsetChanged;
        }

        private async void OffsetSliderOnOffsetChanged(object sender, ValueChangedEventArgs changedEvent)
        {
            
        }

        private async void TreeViewOnDataPointTapped(object sender, IDataPoint dataPoint)
        {
            var timeView = dataPoint.Time.CreateViewer();
            //ImageView.Create(dataPoint, timeView);
        }
    }
}