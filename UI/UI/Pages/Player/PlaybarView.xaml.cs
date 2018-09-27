using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.UI.Figures;
using SINTEF.AutoActive.UI.Views;
using System;
using System.Collections.Generic;
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


        public PlaybarView ()
		{
			InitializeComponent ();
        }

        private DataViewerContext _viewerContext;
        public DataViewerContext ViewerContext
        {
            get => _viewerContext;
            set
            {
                if (_viewerContext != null) _viewerContext.DataRangeUpdated -= ViewerContext_DataRangeUpdated;
                _viewerContext = value;
                if (_viewerContext != null)
                {
                    _viewerContext.DataRangeUpdated += ViewerContext_DataRangeUpdated;
                    ViewerContext_DataRangeUpdated(_viewerContext.HasDataFrom, _viewerContext.HasDataTo);
                }
            }
        }

        private readonly DataViewerContext previewContext = new DataViewerContext(DataViewerRangeType.Time, 0, 0);

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            LabelTimeFrom.Text = SecondsToTimeString(e.NewValue);
            ViewerContext.UpdateRange(e.NewValue, e.NewValue + 100);
        }

        private void ViewerContext_DataRangeUpdated(double from, double to)
        {
            LabelTimeFrom.Text = SecondsToTimeString(from);
            LabelTimeTo.Text = SecondsToTimeString(to);
            if (to <= TimeSlider.Minimum) TimeSlider.Maximum = from + 1;
            else TimeSlider.Maximum = to;
            previewContext.UpdateRange(from, to);
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

        private string SecondsToTimeString(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.ToString(@"hh\:mm\:ss");
        }
    }
}