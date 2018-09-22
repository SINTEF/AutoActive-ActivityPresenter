using Xamarin.Forms;
using SkiaSharp;
using SkiaSharp.Views.Forms;

using System.Runtime.CompilerServices;

using SINTEF.AutoActive.Databus;
using System.Diagnostics;
using SINTEF.AutoActive.UI.Views;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.Common;

namespace SINTEF.AutoActive.UI.Figures
{
    public class LinePlot : FigureView
    {
        public static async Task<LinePlot> Create(IDataPoint datapoint, DataViewerContext context)
        {
            // TODO: Check that this datapoint has a type that can be used
            var viewer = await datapoint.CreateViewerIn(context);
            return new LinePlot(viewer as ITimeSeriesViewer, context);
        }

        protected ITimeSeriesViewer Viewer { get; private set; }

        protected LinePlot(ITimeSeriesViewer viewer, DataViewerContext context) : base(viewer, context)
        {
            Viewer = viewer;

            if (Viewer.DataPoint.DataType == typeof(byte)) CreatePathFunction = CreateBytePath;
            else if (Viewer.DataPoint.DataType == typeof(int)) CreatePathFunction = CreateIntPath;
            else if (Viewer.DataPoint.DataType == typeof(long)) CreatePathFunction = CreateLongPath;
            else if (Viewer.DataPoint.DataType == typeof(float)) CreatePathFunction = CreateFloatPath;
            else if (Viewer.DataPoint.DataType == typeof(double)) CreatePathFunction = CreateDoublePath;
            else CreatePathFunction = EmptyPath;
        }

        // Line drawing
        delegate void CreatePath(SKPath plot);
        CreatePath CreatePathFunction;

        void EmptyPath(SKPath plot) { }
        void CreateBytePath(SKPath plot)
        {
            var en = Viewer.GetCurrentBytes().GetEnumerator();
            if (en.MoveNext())
            {
                plot.MoveTo(en.Current.x, en.Current.y);
                while (en.MoveNext()) plot.LineTo(en.Current.x, en.Current.y);
            }
        }
        void CreateIntPath(SKPath plot)
        {
            var en = Viewer.GetCurrentInts().GetEnumerator();
            if (en.MoveNext())
            {
                plot.MoveTo(en.Current.x, en.Current.y);
                while (en.MoveNext()) plot.LineTo(en.Current.x, en.Current.y);
            }
        }
        void CreateLongPath(SKPath plot)
        {
            var en = Viewer.GetCurrentLongs().GetEnumerator();
            if (en.MoveNext())
            {
                plot.MoveTo(en.Current.x, en.Current.y);
                while (en.MoveNext()) plot.LineTo(en.Current.x, en.Current.y);
            }
        }
        void CreateFloatPath(SKPath plot)
        {
            var en = Viewer.GetCurrentFloats().GetEnumerator();
            if (en.MoveNext())
            {
                plot.MoveTo(en.Current.x, en.Current.y);
                while (en.MoveNext()) plot.LineTo(en.Current.x, en.Current.y);
            }
        }
        void CreateDoublePath(SKPath plot)
        {
            var en = Viewer.GetCurrentDoubles().GetEnumerator();
            if (en.MoveNext())
            {
                plot.MoveTo(en.Current.x, (float)en.Current.y);
                while (en.MoveNext()) plot.LineTo(en.Current.x, (float)en.Current.y);
            }
        }


        protected override void RedrawCanvas(SKCanvas canvas, SKImageInfo info)
        {
            canvas.Clear(SKColors.CornflowerBlue);

            if (Viewer != null)
            {
                var startX = (float)Context.RangeFrom;
                var endX = (float)Context.RangeTo;

                // Scale so that the whole canvas is x=[0,1] y=[-1,1]
                canvas.Scale(info.Width/(endX-startX), -info.Height / 2);
                canvas.Translate(-startX, -1);

                // Create path
                SKPath plot = new SKPath();
                CreatePathFunction(plot);

                // Draw the data
                canvas.DrawPath(plot, new SKPaint() { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 0.01f, StrokeJoin = SKStrokeJoin.Miter, IsAntialias = true });
            }
        }
    }
}
