using System.Linq;
using SkiaSharp;
using System.Runtime.CompilerServices;
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
            if (datapoint.DataType == typeof(byte)) return new ByteLinePlot(viewer, context) {Legend = datapoint.Name};
            if (datapoint.DataType == typeof(int)) return new IntLinePlot(viewer, context) {Legend = datapoint.Name};
            if (datapoint.DataType == typeof(long)) return new LongLinePlot(viewer, context) {Legend = datapoint.Name};
            if (datapoint.DataType == typeof(float)) return new FloatLinePlot(viewer, context) {Legend = datapoint.Name};
            if (datapoint.DataType == typeof(double)) return new DoubleLinePlot(viewer, context) {Legend = datapoint.Name};
            return null;
        }

        protected ITimeSeriesViewer Viewer { get; }

        protected LinePlot(ITimeSeriesViewer viewer, TimeSynchronizedContext context) : base(viewer, context)
        {
            Viewer = viewer;

            if (Viewer.MinValueHint.HasValue) _minYValue = (float)Viewer.MinValueHint.Value;
            if (Viewer.MaxValueHint.HasValue) _maxYValue = (float)Viewer.MaxValueHint.Value;
        }

        protected override void Viewer_Changed_Hook()
        {
            if (Viewer == null)
                return;
            // TODO fix crude autoscaling
            if (Viewer.MinValueHint.HasValue) _minYValue = (float)Viewer.MinValueHint.Value;
            if (Viewer.MaxValueHint.HasValue) _maxYValue = (float)Viewer.MaxValueHint.Value;
        }

        public int MaxItems { get; } = 1000;
        public string Legend;

        // ---- Scaling ----
        private float _minYValue = -1;
        private float _maxYValue = 1;

        public float MinY
        {
            get => _minYValue;
            set
            {
                _minYValue = value;
                Canvas.InvalidateSurface();
            }
        }

        public float MaxY
        {
            get => _maxYValue;
            set
            {
                _maxYValue = value;
                Canvas.InvalidateSurface();
            }
        }

        // ---- Drawing ----
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected new float ScaleX(long v, long offset, float scale)
        {
            return (v - offset) * scale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected new float ScaleY(float v, float offset, float scale)
        {
            return (v - offset) * scale;
        }

        protected abstract void CreatePath(SKPath plot, long offsetX, float scaleX, float offsetY, float scaleY);

        private static readonly SKPaint FramePaint = new SKPaint {
            Color = SKColors.LightSlateGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = false,
        };

        private readonly SKPaint _linePaint = new SKPaint {
            Color = SKColors.OrangeRed,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true,
        };

        private readonly SKPaint _zeroLinePaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new float[] {5,5}, 10)
        };

        private readonly SKPaint _currentLinePaint = new SKPaint
        {
            Color = SKColors.Lime,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true
        };

        private readonly SKPaint _textPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            SubpixelText = true,
        };

        private readonly SKPaint _legendFill = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            StrokeWidth = 1,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true,
        };

        private readonly SKPaint _legendStroke = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true,
        };

        private void DrawLine(SKCanvas canvas, LineConfiguration lineConfig)
        {
            // Create path
            var plot = new SKPath();
            CreatePath(plot, lineConfig.OffsetX, lineConfig.ScaleX, lineConfig.OffsetY, lineConfig.ScaleY);

            // Draw the data
            canvas.DrawPath(plot, lineConfig.LinePaint);
        }

        protected override void RedrawCanvas(SKCanvas canvas, SKImageInfo info)
        {
            // Clear background and draw frame
            canvas.Clear(SKColors.White);
            canvas.DrawRect(0, 0, info.Width-1, info.Height-1, FramePaint);
            
            if (Viewer == null) return;

            if (Viewer.CurrentTimeRangeFrom == Viewer.CurrentTimeRangeTo) return; // Avoid divide-by-zero

            // To achieve a constant line width, we need to scale the data when drawing the path, not scale the whole canvas
            var xDiff = Viewer.CurrentTimeRangeTo - Viewer.CurrentTimeRangeFrom;
            var currentXTime = Viewer.CurrentTimeRangeFrom;
            var startX = currentXTime - xDiff/3;
            var scaleX = (float)info.Width / xDiff;

            var minY = _minYValue;
            var maxY = _maxYValue;
            var scaleY = -info.Height / (maxY - minY);

            // Draw current-y axis
            var zeroX = ScaleX(currentXTime, startX, scaleX);
            canvas.DrawLine(zeroX, 0, zeroX, info.Height, _currentLinePaint);

            // Draw zero-x axis
            var zeroY = ScaleY(0, maxY, scaleY);
            canvas.DrawLine(0, zeroY, info.Width, zeroY, _zeroLinePaint);

            var lineConfig = new LineConfiguration
            {
                LinePaint = _linePaint,
                OffsetX =  startX,
                OffsetY = maxY,
                ScaleX = scaleX,
                ScaleY = scaleY,
                Legend = Legend
            };

            DrawLine(canvas, lineConfig);
            DrawLegends(canvas, info, new [] { lineConfig });
        }

        private void DrawLegends(SKCanvas canvas, SKImageInfo info, LineConfiguration[] configs)
        {
            var frameStartX = info.Width - 1;
            var frameStartY = 1;

            const int legendLineWidth = 10;
            const int legendLineSpacing = 3;
            const int legendPadding = 5;
            const int legendMargin = 5;
            const int multipleLegendSpacing = 5;

            var textHeight = _textPaint.FontMetrics.CapHeight;

            var nLegends = configs.Count(config => config.Legend != null);
            if (nLegends == 0)
            {
                return;
            }

            var maxTextWidth = configs.Max(config => _textPaint.MeasureText(config.Legend));

            var legendTextStartX = frameStartX - (legendMargin + legendPadding + maxTextWidth);

            var legendFrameStartY = frameStartY + legendMargin;
            var legendTextStartY = legendFrameStartY + textHeight + legendPadding;

            var legendLineX1 = legendTextStartX - legendLineSpacing;
            var legendLineX0 = legendLineX1 - legendLineWidth;
            
            var legendFrameStartX = legendLineX0 - legendPadding;
            var legendFrameWidth = info.Width - legendMargin - legendFrameStartX;
            var legendEndY = legendTextStartY + legendPadding;

            var legendYDelta = multipleLegendSpacing + textHeight;
            var legendFrameHeight = legendEndY - (frameStartY + legendMargin) + (nLegends - 1) * legendYDelta;

            canvas.DrawRect(legendFrameStartX, legendFrameStartY, legendFrameWidth, legendFrameHeight, _legendFill);
            canvas.DrawRect(legendFrameStartX, legendFrameStartY, legendFrameWidth, legendFrameHeight, _legendStroke);

            var legendIx = 0;
            foreach (var config in configs)
            {
                if (config.Legend == null)
                    continue;

                var text = config.Legend;

                var yPos = legendTextStartY + legendYDelta * legendIx;

                var legendLineY = yPos - textHeight / 2;
                
                canvas.DrawText(text, legendTextStartX, yPos, _textPaint);
                canvas.DrawLine(legendLineX0, legendLineY, legendLineX1, legendLineY, config.LinePaint);
                legendIx++;
            }
        }
    }

    public struct LineConfiguration
    {
        public long OffsetX;
        public float ScaleX;
        public float OffsetY;
        public float ScaleY;
        public SKPaint LinePaint;
        public string Legend;
    }
}
