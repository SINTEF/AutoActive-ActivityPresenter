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
using SINTEF.AutoActive.Databus.ViewerContext;

namespace SINTEF.AutoActive.UI.Figures
{
    public abstract class LinePlot : FigureView
    {
        public static async Task<LinePlot> Create(IDataPoint datapoint, TimeSynchronizedContext context)
        {
            // TODO: Check that this datapoint has a type that can be used
            var viewer = await context.GetDataViewerFor(datapoint) as ITimeSeriesViewer;

            // Use the correct path drawing function
            if (datapoint.DataType == typeof(byte)) return new ByteLinePlot(viewer, context);
            else if (datapoint.DataType == typeof(int)) return new IntLinePlot(viewer, context);
            else if (datapoint.DataType == typeof(long)) return new LongLinePlot(viewer, context);
            else if (datapoint.DataType == typeof(float)) return new FloatLinePlot(viewer, context);
            else if (datapoint.DataType == typeof(double)) return new DoubleLinePlot(viewer, context);
            else return null;
        }

        protected ITimeSeriesViewer Viewer { get; private set; }

        protected LinePlot(ITimeSeriesViewer viewer, TimeSynchronizedContext context) : base(viewer, context)
        {
            Viewer = viewer;

            if (Viewer.MinValueHint.HasValue) minYValue = (float)Viewer.MinValueHint.Value;
            if (Viewer.MaxValueHint.HasValue) maxYValue = (float)Viewer.MaxValueHint.Value;
        }

        protected override void Viewer_Changed_Hook()
        {
            // TODO fix crude autoscaling
            if (Viewer.MinValueHint.HasValue) minYValue = (float)Viewer.MinValueHint.Value;
            if (Viewer.MaxValueHint.HasValue) maxYValue = (float)Viewer.MaxValueHint.Value;
        }

        public int MaxItems { get; } = 1000;

        // ---- Scaling ----
        private float minYValue = -1;
        private float maxYValue = 1;

        public float MinY
        {
            get => minYValue;
            set
            {
                minYValue = value;
                Canvas.InvalidateSurface();
            }
        }

        public float MaxY
        {
            get => maxYValue;
            set
            {
                maxYValue = value;
                Canvas.InvalidateSurface();
            }
        }

        // ---- Drawing ----
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected float ScaleX(long v, long offset, float scale)
        {
            return (v - offset) * scale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected float ScaleY(float v, float offset, float scale)
        {
            return (v - offset) * scale;
        }

        protected abstract void CreatePath(SKPath plot, long offsetX, float scaleX, float offsetY, float scaleY);

        static readonly SKPaint FramePaint = new SKPaint {
            Color = SKColors.LightSlateGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = false,
        };
        SKPaint LinePaint = new SKPaint {
            Color = SKColors.OrangeRed,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true,
        };

        protected override void RedrawCanvas(SKCanvas canvas, SKImageInfo info)
        {
            // Clear background and draw frame
            canvas.Clear(SKColors.White);
            canvas.DrawRect(0, 0, info.Width-1, info.Height-1, FramePaint);

            if (Viewer != null)
            {
                if (Viewer.CurrentTimeRangeFrom == Viewer.CurrentTimeRangeTo) return; // Avoid divide-by-zero

                // To acheive a constant line width, we need to scale the data when drawing the path, not scale the whole canvas
                var startX = Viewer.CurrentTimeRangeFrom;
                var scaleX = (float)info.Width / (Viewer.CurrentTimeRangeTo - Viewer.CurrentTimeRangeFrom);

                var minY = minYValue;
                var maxY = maxYValue;
                var scaleY = info.Height / (maxY - minY);

                // Create path
                SKPath plot = new SKPath();
                CreatePath(plot, startX, scaleX, maxY, -scaleY);

                // Draw the data
                canvas.DrawPath(plot, LinePaint);
            }
        }
    }
}
