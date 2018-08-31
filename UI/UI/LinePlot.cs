using Xamarin.Forms;
using SkiaSharp;
using SkiaSharp.Views.Forms;

using System.Runtime.CompilerServices;

using SINTEF.AutoActive.Databus;
using System.Diagnostics;

namespace SINTEF.AutoActive.UI
{
    public class LinePlot : SKCanvasView
    {
        public static readonly BindableProperty DataProperty = BindableProperty.Create(
            propertyName: "Data",
            returnType: typeof(IDataPoint),
            declaringType: typeof(LinePlot),
            defaultValue: null
        );

        public static readonly BindableProperty ViewerContextProperty = BindableProperty.Create(
            propertyName: "ViewerContext",
            returnType: typeof(DataViewerContext),
            declaringType: typeof(LinePlot),
            defaultValue: null
        );

        public IDataPoint Data
        {
            get { return (IDataPoint)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        public DataViewerContext ViewerContext
        {
            get { return (DataViewerContext)GetValue(ViewerContextProperty); }
            set { SetValue(ViewerContextProperty, value); }
        }

        private IDataViewer viewer;

        protected async override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == "Data" || propertyName == "ViewerContext")
            {
                if (Data != null && ViewerContext != null)
                {
                    viewer = await Data.CreateViewerIn(ViewerContext);
                    viewer.Changed += () =>
                    {
                        InvalidateSurface();
                    };
                }
            }

        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.CornflowerBlue);

            if (viewer != null)
            {
                // Scale so that the whole canvas is x=[0,1] y=[-1,1]
                canvas.Scale(e.Info.Width, -e.Info.Height / 2);
                canvas.Translate(0, -1);

                // Draw the data
                SKPath plot = new SKPath();

                var data = viewer.GetCurrentFloat();
                var en = data.GetEnumerator();

                var count = 0;
                var width = 0f;
                
                if (en.MoveNext())
                {
                    // Move to first point
                    var first = en.Current;
                    plot.MoveTo(0, first.y);
                    count = 1;

                    // Draw the rest
                    while (en.MoveNext())
                    {
                        var (x, y) = en.Current;
                        plot.LineTo((x - first.x)/100, y/10);
                        count++;
                        width = x - first.x;
                    }

                }
                //canvas.ResetMatrix();
                //canvas.Scale(1, 1f/2);

                canvas.DrawPath(plot, new SKPaint() { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 0.01f, StrokeJoin = SKStrokeJoin.Miter, IsAntialias = true });
            }
        }
    }
}
