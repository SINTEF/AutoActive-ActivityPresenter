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
using System.Threading.Tasks;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Figures
{
    public class LinePlot : FigureView
    {
        private readonly List<LineConfiguration> _lines = new List<LineConfiguration>();

        private readonly TimeSynchronizedContext _context;

        public static async Task<LinePlot> Create(IDataPoint datapoint, TimeSynchronizedContext context)
        {
            var linePlot = new LinePlot(null, context);

            var lineDrawer = await linePlot.CreateLineDrawer(datapoint);
            linePlot.AddLine(lineDrawer, datapoint.Name);
            return linePlot;
        }

        private float? _minYValue;
        private float? _maxYValue;

        public async void AddLine(IDataPoint datapoint)
        {
            AddLine(await CreateLineDrawer(datapoint), datapoint.Name);
        }
        public void AddLine(ILineDrawer lineDrawer, string legend)
        {
            lineDrawer.Parent = this;

            
            _lines.Add(new LineConfiguration()
            {
                Drawer = lineDrawer,
                LinePaint = LinePaintProvider.GetNextPaint()
            });
            
            _minYValue = _lines.Max(line => line.Drawer.MinY);
            _maxYValue = _lines.Max(line => line.Drawer.MaxY);

            var yDelta = _maxYValue.Value - _minYValue.Value;
            foreach (var line in _lines)
            {
                line.YDelta = yDelta;
                line.OffsetY = _maxYValue.Value;
            }
        }

        public ILinePaintProvider LinePaintProvider { get; set; } = new MatPlotLib2LinePaint();

        public async Task<ILineDrawer> CreateLineDrawer(IDataPoint dataPoint)
        {
            var viewer = await _context.GetDataViewerFor(dataPoint) as ITimeSeriesViewer;

            if (!dataPoint.GetType().IsGenericType || viewer == null) return null;

            var args = new object[] {viewer};

            var genericConstructor = typeof(LineDrawer<>).MakeGenericType(dataPoint.DataType)
                .GetConstructor(args.Select(a => a.GetType()).ToArray());

            if (genericConstructor == null)
            {
                Debug.WriteLine(
                    "Could not find LineDrawer constructor. Make sure it is public and that the specified arguments are correct.");
            }

            var lineDrawer = (ILineDrawer) genericConstructor?.Invoke(args);
            if (lineDrawer != null)
            {
                lineDrawer.Legend = dataPoint.Name;
            }
            return lineDrawer;
        }

        protected LinePlot(ITimeSeriesViewer viewer, TimeSynchronizedContext context) : base(viewer, context)
        {
            _context = context;
        }
        
        public static int MaxPlotPoints { get; } = 1000;

        // ---- Drawing ----
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new static float ScaleX(long v, long offset, float scale)
        {
            return (v - offset) * scale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new static float ScaleY(float v, float offset, float scale)
        {
            return (v - offset) * scale;
        }

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
            lineConfig.Drawer.CreatePath(plot, lineConfig);

            // Draw the data
            canvas.DrawPath(plot, lineConfig.LinePaint);
        }

        protected override void RedrawCanvas(SKCanvas canvas, SKImageInfo info)
        {
            // Clear background and draw frame
            canvas.Clear(SKColors.White);
            canvas.DrawRect(0, 0, info.Width-1, info.Height-1, FramePaint);

            ITimeSeriesViewer firstViewer;

            //TODO: choose first x and last x instead?
            var xDiff = 0L;
            var firstStartTime = long.MaxValue;
            foreach (var line in _lines)
            {
                firstViewer = line.Drawer.Viewer;
                // To achieve a constant line width, we need to scale the data when drawing the path, not scale the whole canvas
                xDiff = firstViewer.CurrentTimeRangeTo - firstViewer.CurrentTimeRangeFrom;
                if (xDiff == 0) continue;

                firstStartTime = firstViewer.CurrentTimeRangeFrom;
                break;
            }
            
            if (xDiff == 0) return; // No data selected -> avoid divide-by-zero

            var currentXTime = firstStartTime;
            var startX = currentXTime - xDiff/3;
            var scaleX = (float)info.Width / xDiff;
            
            foreach (var line in _lines)
            {
                line.OffsetX = startX;
                line.ScaleX = scaleX;
                line.ScaleY = -info.Height / (line.YDelta);
            }

            // Draw current-y axis
            var zeroX = ScaleX(currentXTime, startX, scaleX);
            canvas.DrawLine(zeroX, 0, zeroX, info.Height, _currentLinePaint);

            // Draw zero-x axis
            var zeroY = ScaleY(0, _lines.First().OffsetY, _lines.First().ScaleY);
            canvas.DrawLine(0, zeroY, info.Width, zeroY, _zeroLinePaint);

            foreach(var lineConfig in _lines) {
                DrawLine(canvas, lineConfig);
            }
            DrawLegends(canvas, info, _lines);
        }

        private void DrawLegends(SKCanvas canvas, SKImageInfo info, IReadOnlyCollection<LineConfiguration> configs)
        {
            var frameStartX = info.Width - 1;
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
            var legendFrameWidth = info.Width - legendMargin - legendFrameStartX;
            var legendEndY = legendTextStartY + legendPadding;

            var legendYDelta = multipleLegendSpacing + textHeight;
            var legendFrameHeight = legendEndY - (frameStartY + legendMargin) + (nLegends - 1) * legendYDelta;

            canvas.DrawRect(legendFrameStartX, legendFrameStartY, legendFrameWidth, legendFrameHeight, _legendFill);
            canvas.DrawRect(legendFrameStartX, legendFrameStartY, legendFrameWidth, legendFrameHeight, _legendStroke);

            var legendIx = 0;
            foreach (var config in configs)
            {
                if (config.Drawer.Legend == null)
                    continue;

                var text = config.Drawer.Legend;

                var yPos = legendTextStartY + legendYDelta * legendIx;

                var legendLineY = yPos - textHeight / 2;
                
                canvas.DrawText(text, legendTextStartX, yPos, TextPaint);
                canvas.DrawLine(legendLineX0, legendLineY, legendLineX1, legendLineY, config.LinePaint);
                legendIx++;
            }
        }

        public void InvalidateSurface()
        {
            Canvas.InvalidateSurface();
        }

        protected const string AddLineText = "Add Line";
        protected const string RemoveLineText = "Remove Line";

        protected override string[] GetExtraMenuParameters()
        {
            return _lines.Count == 1 ? new[] {AddLineText} : new[] {AddLineText, RemoveLineText};
        }

        private List<IDataPoint> GetAllDataPoints(IEnumerable<IDataStructure> dataStructures)
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


        protected override async void OnHandleMenuResult(Page page, string action)
        {
            switch (action)
            {
                case AddLineText:
                    var dataPoints = GetAllDataPoints(Databus.DataRegistry.Providers);

                    var newLineName = await page.DisplayActionSheet("Add Line", CancelText, null,
                        dataPoints.Select(child => child.Name).ToArray());
                    if (newLineName == CancelText)
                        return;
                    var dataPoint = dataPoints.First(dp => dp.Name == newLineName);
                    AddLine(dataPoint);
                    return;
                case RemoveLineText:
                    var lineToRemoveAction = await page.DisplayActionSheet("Remove Line", CancelText, null,
                        _lines.Select(line => line.Drawer.Legend).ToArray());
                    if (lineToRemoveAction == CancelText)
                        return;

                    var toRemove = _lines.Where(line => line.Drawer.Legend == lineToRemoveAction);
                    foreach (var line in toRemove)
                    {
                        _context.Remove(line.Drawer.Viewer);
                    }
                    _lines.RemoveAll(line => line.Drawer.Legend == lineToRemoveAction);
                    return;
                case RemoveText:
                    foreach (var line in _lines)
                    {
                        _context.Remove(line.Drawer.Viewer);
                    }
                    break;
                default:
                    break;
            }
            base.OnHandleMenuResult(page, action);
        }
    }

    public class LineConfiguration
    {
        public ILineDrawer Drawer { get; set; }

        public long OffsetX { get; set; }
        public float ScaleX { get; set; }
        public float YDelta { get; set; }

        public float OffsetY { get; set; }
        public float ScaleY { get; set; }
        public SKPaint LinePaint { get; set; }
    }
}
