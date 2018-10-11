using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Figures;
using SINTEF.AutoActive.UI.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Pages.Player
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class PlaybarView : ContentView
	{
        public static readonly GridLength DefaultHeight = 40;
        public static readonly GridLength DefaultPreviewHeight = 100;

        public PlaybarView (DataViewerContext context)
		{
			InitializeComponent ();

            ViewerContext = context;
            context.AvailableTimeRangeChanged += ViewerContext_AvailableTimeRangeChanged;
            ViewerContext_AvailableTimeRangeChanged(context, context.AvailableTimeFrom, context.AvailableTimeTo);
		}

        public DataViewerContext ViewerContext { get; private set; }
        private readonly TimeSynchronizedContext previewContext = new TimeSynchronizedContext();

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            var startPoint = (long)(e.NewValue / 10000 * (ViewerContext.AvailableTimeTo - ViewerContext.AvailableTimeFrom)) + ViewerContext.AvailableTimeFrom;

            if (ViewerContext is TimeSynchronizedContext timeContext)
            {
                Debug.WriteLine($"Playbar Slider startpoint: {startPoint}");
                timeContext.SetSelectedTimeRange(startPoint, startPoint + 1000000*100); // 100s
            }
            // FIXME: Handle the other types of context
        }

        //private double lastFrom = 0;
        //private double? lastTo = 0 ;

        private void ViewerContext_AvailableTimeRangeChanged(DataViewerContext sender, long from, long to)

        {
            Device.BeginInvokeOnMainThread(() => 
            {
            Debug.WriteLine($"Playbar AVAILABLE TIME {from}->{to}");
            LabelTimeFrom.Text = from.ToString();
            LabelTimeTo.Text = to.ToString();
            previewContext.SetSelectedTimeRange(from, to);


                //if (lastTo < to)
                //{
                //    TimeSlider.Value = to - 100;
                //}
                //lastFrom = from;
                //lastTo = to;

            });
        }

        /* --- Public API --- */
        public IDataPoint PreviewDataPoint { get; private set; }
        private FigureView previewView;

        public async void UseDataPointForTimelinePreview(IDataPoint datapoint)
        {
            if (PreviewDataPoint == null)
            {
                RowDataPreview.Height = DefaultPreviewHeight;
            }
            PreviewDataPoint = datapoint;

            var plot = await LinePlot.Create(datapoint, previewContext);
            ContentGrid.Children.Add(plot, 0, 3, 0, 1);
            if (previewView != null) ContentGrid.Children.Remove(previewView);
            previewView = plot;
        }
    }
}