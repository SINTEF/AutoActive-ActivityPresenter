using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Implementations.TabularStructure;
using SINTEF.AutoActive.Plugins.ArchivePlugins.Video;
using SINTEF.AutoActive.Plugins.Import.Mqtt;
using SINTEF.AutoActive.UI.Figures;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views
{
	public abstract partial class FigureView : ContentView
	{
        private IDataViewer Viewer { get; set; }
        protected TimeSynchronizedContext Context { get; private set; }

        protected FigureView(IDataViewer viewer, TimeSynchronizedContext context)
        {
            InitializeComponent();

            Viewer = viewer;
            Context = context;

            // Redraw canvas when data changes, size of figure changes, or range updates
            // FIXME: This usually causes at least double updates. Maybe we can sort that out somehow
            // It really doesn't make sense to redraw more often than the screen refresh-rate either way
            viewer.Changed += Viewer_Changed;
            SizeChanged += FigureView_SizeChanged;
            Context.SelectedTimeRangeChanged += Context_SelectedTimeRangeChanged;
            Canvas.PaintSurface += Canvas_PaintSurface;
            
        }

        private void FigureView_SizeChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("FigureView::FigureView_SizeChanged ");
            Canvas.InvalidateSurface();
        }

        public void Viewer_Tapped(object sender, EventArgs e)
        {
            Debug.WriteLine("FigureView::Viewer_tapped");
        }

        public void Viewer_Panned(object sender, PanUpdatedEventArgs e)
        {
            string debugText = "FigureView::Viewer_Panned ";
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    debugText += "STARTED";
                    break;
                case GestureStatus.Running:
                    debugText += "RUNNING";
                    break;
                case GestureStatus.Completed:
                    debugText += "COMPLETED";
                    break;
                case GestureStatus.Canceled:
                    debugText += "CANCELED";
                    break;
            }
            Debug.WriteLine(debugText);
            Debug.WriteLine($"X:{e.TotalX} Y:{e.TotalY}");

        }

        //public void Viewer_Swiped(object sender, SwipedEventArgs e)
        //{
        //string debugText = "FigureView::Viewer_Swiped ";
        //    switch (e.Direction)
        //    {
        //        case SwipeDirection.Left:
        //debugText += "LEFT";
        //            break;
        //        case SwipeDirection.Right:
        //debugText += "RUNNING";
        //            break;
        //        case SwipeDirection.Up:
        //debugText += "UP";
        //            break;
        //        case SwipeDirection.Down:
        //debugText += "DOWN";
        //            break;
        //}
        //Debug.WriteLine(debugText);
        //
        //    }

        private void Viewer_Changed(IDataViewer sender)
        {
            //Debug.WriteLine("FigureView::Viewer_Changed ");
            Canvas.InvalidateSurface();
            Viewer_Changed_Hook();
        }

        protected virtual void Viewer_Changed_Hook()
        {
            // Hook method to be overridden by sub classes if special handling needed
        }

        private void Context_SelectedTimeRangeChanged(SingleSetDataViewerContext sender, long from, long to)
        {
            //Debug.WriteLine("FigureView::Context_RangeUpdated " + from + " " + to );
            Canvas.InvalidateSurface();
        }

        private void Canvas_PaintSurface(object sender, SkiaSharp.Views.Forms.SKPaintSurfaceEventArgs e)
        {
            //Debug.WriteLine("FigureView::Canvas_PaintSurface ");
            RedrawCanvas(e.Surface.Canvas, e.Info);
        }

        protected abstract void RedrawCanvas(SKCanvas canvas, SKImageInfo info);


	    public static async Task<FigureView> GetView(IDataPoint datapoint, TimeSynchronizedContext context)
	    {
	        FigureView view;
	        switch (datapoint)
	        {
	            case ArchiveVideoVideo _:
	                view = await ImageView.Create(datapoint, context);
	                break;
	            case TableColumn _:
	            case TableColumnDyn _:
	                view = await LinePlot.Create(datapoint, context);
	                break;
	            default:
	                throw new NotSupportedException();
	        }

	        return view;
	    }
    }
}