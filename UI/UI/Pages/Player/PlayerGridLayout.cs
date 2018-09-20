using SINTEF.AutoActive.Databus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    //public class PlayerGridLayout : Layout<View>
    public class PlayerGridLayout : StackLayout
    {
        // FIXME : Implement this class, and also possibly restrict this to more specific views for data-renderers
        public PlayerGridLayout()
        {
        }

        public DataViewerContext ViewerContext { get; set; }
        
        /*
        protected override void LayoutChildren(double x, double y, double width, double height)
        {
            foreach (var child in Children)
            {
                child.Layout(new Rectangle(x, y, width, height));
            }
        }
        */

        public void AddPlotFor(IDataPoint datapoint)
        {
            var plot = new LinePlot
            {
                Data = datapoint,
                ViewerContext = ViewerContext,
                HeightRequest = 100,
            };
            Children.Add(plot);
        }
    }
}
