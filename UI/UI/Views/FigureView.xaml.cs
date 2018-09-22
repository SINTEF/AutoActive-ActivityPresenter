using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Interfaces;
using SkiaSharp;
using System;
using System.Collections.Generic;
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
        protected DataViewerContext Context { get; private set; }

		protected FigureView(IDataViewer viewer, DataViewerContext context)
		{
			InitializeComponent ();

            Viewer = viewer;
            Context = context;

            // Redraw canvas when data changes, or size of figure changes
            viewer.Changed += Viewer_Changed;
            SizeChanged += FigureView_SizeChanged;
            Canvas.PaintSurface += Canvas_PaintSurface;
		}

        private void FigureView_SizeChanged(object sender, EventArgs e)
        {
            Canvas.InvalidateSurface();
        }

        private void Viewer_Changed()
        {
            Canvas.InvalidateSurface();
        }

        private void Canvas_PaintSurface(object sender, SkiaSharp.Views.Forms.SKPaintSurfaceEventArgs e)
        {
            RedrawCanvas(e.Surface.Canvas, e.Info);
        }

        protected abstract void RedrawCanvas(SKCanvas canvas, SKImageInfo info);
    }
}