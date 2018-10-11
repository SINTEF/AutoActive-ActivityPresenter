using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
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
            Canvas.InvalidateSurface();
        }

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
    }
}