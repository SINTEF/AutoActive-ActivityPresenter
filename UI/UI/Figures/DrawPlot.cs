using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Figures.LinePaintProviders;
using SINTEF.AutoActive.UI.Views;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Figures
{
    public abstract class DrawPlot : FigureView
    {

        protected readonly List<LineConfiguration> _lines = new List<LineConfiguration>();

        protected readonly TimeSynchronizedContext _context;
        protected float? _minYValue;
        protected float? _maxYValue;

        /// Update axis range and scaling for this plot.
        /// \pre _lines cannot be empty when calling this.
        protected void UpdateLineData()
        {

            _minYValue = _lines.Min(line => line.Drawer.MinY);
            _maxYValue = _lines.Max(line => line.Drawer.MaxY);

            var yDelta = _maxYValue.Value - _minYValue.Value;
            foreach (var line in _lines)
            {
                line.YDelta = yDelta;
                line.OffsetY = _maxYValue.Value;
            }
        }

        public async Task AddLine(IDataPoint datapoint)
        {
            var line = await CreateLineDrawer(datapoint);
            if (line == null)
            {
                throw new ArgumentException("Could not create line");
            }

            AddLine(line);

            DataPoints.Add(datapoint);

            var container = XamarinHelpers.GetFigureContainerFromParents(Parent);
            container?.InvokeDatapointAdded(datapoint, _context);
        }

        protected void AddLine(ILineDrawer lineDrawer)
        {
            lineDrawer.Parent = this;
            _lines.Add(new LineConfiguration(this)
            {
                Drawer = lineDrawer,
                LinePaint = LinePaintProvider.GetNextPaint()
            });

            UpdateLineData();

            // Trigger TimeViewer selected time for the new line
            _context.SetSelectedTimeRange(_context.SelectedTimeFrom, _context.SelectedTimeTo);

            InvalidateSurface();
        }

        public ILinePaintProvider LinePaintProvider { get; set; } = new MatPlotLib2LinePaint();

        public async Task<ILineDrawer> CreateLineDrawer(IDataPoint dataPoint)
        {
            if (_lines.Any(lp => lp.Drawer.Viewer.DataPoint == dataPoint))
            {
                throw new ArgumentException("Line already in plot");
            }

            if (!dataPoint.GetType().IsGenericType) return null;

            var genericConstructor = typeof(LineDrawer<>).MakeGenericType(dataPoint.DataType)
                .GetConstructor(new[] { typeof(ITimeSeriesViewer) });
            if (genericConstructor == null)
            {
                Debug.WriteLine(
                    "Could not find LineDrawer constructor. Make sure it is public and that the specified arguments are correct.");
                return null;
            }

            if (!(await _context.GetDataViewerFor(dataPoint) is ITimeSeriesViewer viewer)) return null;
            AddViewer(viewer);

            viewer.SetTimeRange(_context.SelectedTimeFrom, _context.SelectedTimeTo);
            viewer.PreviewPercentage = PreviewPercentage;
            var lineDrawer = (ILineDrawer)genericConstructor.Invoke(new object[] { viewer });
            if (lineDrawer == null) return null;

            lineDrawer.Legend = string.IsNullOrEmpty(dataPoint.Unit) ? dataPoint.Name : $"{dataPoint.Name} [{dataPoint.Unit}]";

            return lineDrawer;
        }

        public DrawPlot(TimeSynchronizedContext context, IDataPoint dataPoint) : base(context, dataPoint)
        {
            _context = context;
            PlotType = PlotTypes.Line;
        }

        // ---- Drawing ----
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ScalePointX(long v, long offset, float scale)
        {
            return (v - offset) * scale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ScalePointY(float v, float offset, float scale)
        {
            return (v - offset) * scale;
        }

        protected readonly SKPaint _zeroLinePaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 10)
        };

        protected readonly SKPaint _currentLinePaint = new SKPaint
        {
            Color = SKColor.Parse("#F1304D"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true
        };

        protected readonly SKPaint _legendFill = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            StrokeWidth = 1,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true,
        };

        protected readonly SKPaint _legendStroke = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true,
        };

        protected readonly SKPaint TickTextPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            SubpixelText = true,
            TextSize = 10,
        };

        protected void DrawLine(SKCanvas canvas, SKRect drawRect, LineConfiguration lineConfig)
        {
            // Create path
            var plot = new SKPath();
            lineConfig.Drawer.CreatePath(plot, drawRect, lineConfig);

            // Draw the data
            canvas.DrawPath(plot, lineConfig.LinePaint);
        }

        public long PreviewPercentage = 30;

        protected const int TickBoxMargin = 45;
        protected const int TickLength = 3;
        protected const int TickMargin = 3;
        protected const int PlotHeightMargin = 10;

        public static int MaxPlotPoints { get; } = 500;
        public bool CurrentTimeVisible { get; set; } = true;
        public PlotTypes PlotType { get; protected set; }

        protected const int SmoothScalingQueueSize = 30;
        protected readonly Queue<(float, float)> _smoothScalingQueue = new Queue<(float, float)>(SmoothScalingQueueSize);

        internal static int MaxPointsFromWidth(float width)
        {
            return Math.Min((int)width / 2, MaxPlotPoints);
        }

        public void InvalidateSurface()
        {
            Canvas.InvalidateSurface();
        }

        protected void DrawLegends(SKCanvas canvas, SKRect drawRect, IReadOnlyCollection<LineConfiguration> configs)
        {
            var frameStartX = drawRect.Width - 1;
            var frameStartY = 1;

            const int legendLineWidth = 10;
            const int legendLineSpacing = 3;
            const int legendPadding = 5;
            const int legendMargin = 5;
            const int multipleLegendSpacing = 5;

            var textHeight = TextPaint.FontMetrics.CapHeight;

            var nLegends = configs.Count(config => config.Drawer.Legend != null);
            if (nLegends == 0)
            {
                return;
            }

            var maxTextWidth = configs.Max(config => TextPaint.MeasureText(config.Drawer.Legend));

            var legendTextStartX = frameStartX - (legendMargin + legendPadding + maxTextWidth);

            var legendFrameStartY = frameStartY + legendMargin;
            var legendTextStartY = legendFrameStartY + textHeight + legendPadding;

            var legendLineX1 = legendTextStartX - legendLineSpacing;
            var legendLineX0 = legendLineX1 - legendLineWidth;

            var legendFrameStartX = legendLineX0 - legendPadding;
            var legendFrameWidth = drawRect.Width - legendMargin - legendFrameStartX;
            var legendEndY = legendTextStartY + legendPadding;

            var legendYDelta = multipleLegendSpacing + textHeight;
            var legendFrameHeight = legendEndY - (frameStartY + legendMargin) + (nLegends - 1) * legendYDelta;

            canvas.DrawRect(legendFrameStartX + drawRect.Left, legendFrameStartY, legendFrameWidth, legendFrameHeight, _legendFill);
            canvas.DrawRect(legendFrameStartX + drawRect.Left, legendFrameStartY, legendFrameWidth, legendFrameHeight, _legendStroke);

            var legendIx = 0;
            foreach (var config in configs)
            {
                if (config.Drawer.Legend == null)
                    continue;

                var text = config.Drawer.Legend;

                var yPos = legendTextStartY + legendYDelta * legendIx;

                var legendLineY = yPos - textHeight / 2;

                canvas.DrawText(text, legendTextStartX + drawRect.Left, yPos, TextPaint);
                canvas.DrawLine(legendLineX0 + drawRect.Left, legendLineY, legendLineX1 + drawRect.Left, legendLineY, config.LinePaint);
                legendIx++;
            }
        }


        protected List<IDataPoint> GetAllDataPoints(IEnumerable<IDataStructure> dataStructures)
        {
            var dataPoints = new List<IDataPoint>();
            var stack = new Stack<IDataStructure>(dataStructures);
            while (stack.Any())
            {
                var el = stack.Pop();
                dataPoints.AddRange(el.DataPoints);
                foreach (var child in el.Children)
                {
                    stack.Push(child);
                }
            }

            return dataPoints;
        }

        /// Add new datapoint to plot, or remove it if already present in the plot.
        public override async Task<ToggleResult> ToggleDataPoint(IDataPoint datapoint, TimeSynchronizedContext timeContext)
        {
            var existing = FindLines(datapoint);
            if (existing.Count == 0)
            {
                await AddLine(datapoint);
                return ToggleResult.Added;
            }

            RemoveLines(existing);
            return ToggleResult.Removed;
        }

        /// Remove datapoint from plot if present here.
        protected override void RemoveDataPoint(IDataPoint datapoint)
        {
            RemoveLines(FindLines(datapoint));
        }

        /// Find lines showing datapoint.
        /// \note Normally, the list returned does not contain more than one line.
        protected List<LineConfiguration> FindLines(IDataPoint datapoint)
        {
            return _lines.FindAll(lp => lp.Drawer.Viewer.DataPoint == datapoint);
        }

        /// Remove lines from plot, and remove plot if the last line is removed.
        protected void RemoveLines(IReadOnlyCollection<LineConfiguration> linesToRemove)
        {
            if (linesToRemove.Count == 0)
                return;

            var container = XamarinHelpers.GetFigureContainerFromParents(Parent);
            foreach (var line in linesToRemove)
            {
                var dataPoint = line.Drawer.Viewer.DataPoint;
                DataPoints.Remove(dataPoint);
                container?.InvokeDatapointRemoved(dataPoint, _context);
                RemoveViewer(line.Drawer.Viewer);
                _lines.Remove(line);
            }
            if (_lines.Count == 0)
                RemoveThisView();
            else
                UpdateLineData();
            InvalidateSurface();
        }

        /// <summary>
        /// Calculates the scale between the data and pixels. Be aware that the marigns are subtracted, 
        /// the min and the max must therefore be of the data
        /// </summary>
        protected static float YScaleFromDiff(float yMin, float yMax, int height)
        {
            return -(height - PlotHeightMargin * 2) / (yMax - yMin);
        }

    }
    public class LineConfiguration
    {
        public LineConfiguration(DrawPlot drawPlot)
        {
            _linePlot = drawPlot;
        }
        public ILineDrawer Drawer { get; set; }

        public long OffsetX { get; set; }
        public float ScaleX { get; set; }
        public float YDelta { get; set; }

        public float OffsetY { get; set; }
        public float ScaleY { get; set; }
        public SKPaint LinePaint { get; set; }
        public PlotTypes PlotType => _linePlot.PlotType;

        public Queue<(float, float)> SmoothScalingQueue;
        private DrawPlot _linePlot;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PlotTypes
    {
        Line, Scatter, Column
    }
}