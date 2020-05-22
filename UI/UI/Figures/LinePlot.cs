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
using SINTEF.AutoActive.UI.Interfaces;
using Xamarin.Forms;
using ITimeSeriesViewer = SINTEF.AutoActive.Databus.Common.ITimeSeriesViewer;

namespace SINTEF.AutoActive.UI.Figures
{
    public class LinePlot : FigureView
    {
        private readonly List<LineConfiguration> _lines = new List<LineConfiguration>();

        private readonly TimeSynchronizedContext _context;

        public static async Task<LinePlot> Create(IDataPoint datapoint, TimeSynchronizedContext context)
        {
            var linePlot = new LinePlot(context, datapoint);

            var lineDrawer = await linePlot.CreateLineDrawer(datapoint);
            linePlot.AddLine(lineDrawer);
            return linePlot;
        }

        private float? _minYValue;
        private float? _maxYValue;

        /// Update axis range and scaling for this plot.
        /// \pre _lines cannot be empty when calling this.
        private void UpdateLineData()
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

        private void AddLine(ILineDrawer lineDrawer)
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
            var lineDrawer = (ILineDrawer) genericConstructor.Invoke(new object[] { viewer });
            if (lineDrawer == null) return null;

            lineDrawer.Legend = string.IsNullOrEmpty(dataPoint.Unit) ? dataPoint.Name : $"{dataPoint.Name} [{dataPoint.Unit}]";

            return lineDrawer;
        }

        protected LinePlot(TimeSynchronizedContext context, IDataPoint dataPoint) : base(context, dataPoint)
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
            Color = SKColor.Parse("#F1304D"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
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

        protected readonly SKPaint TickTextPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            SubpixelText = true,
            TextSize = 10,
        };


        private void DrawLine(SKCanvas canvas, SKRect drawRect, LineConfiguration lineConfig)
        {
            // Create path
            var plot = new SKPath();
            lineConfig.Drawer.CreatePath(plot, drawRect, lineConfig);

            // Draw the data
            canvas.DrawPath(plot, lineConfig.LinePaint);
        }

        private bool _axisValuesVisible = true;

        public bool AxisValuesVisible
        {
            get => _axisValuesVisible && !_autoScaleIndependent;
            set => _axisValuesVisible = value;
        }
        public static int MaxPlotPoints { get; } = 500;
        public bool CurrentTimeVisible { get; set; } = true;
        public PlotTypes PlotType { get; private set; }

        public long PreviewPercentage = 30;

        private const int TickBoxMargin = 45;
        private const int TickLength = 3;
        private const int TickMargin = 3;
        private const int PlotHeightMargin = 4;

        public bool AutoScale = true;
        private double _previouseWindowHeight = 0;
        private double _previouseWindowWidth = 0;
        private bool _previouseallNumbAreInts = true;
        private bool _autoScaleIndependent;
        private bool _scalingFrozen;
        private (float? minYValue, float? maxYValue) _prevYValue;
        private const int SmoothScalingQueueSize = 30;
        private readonly Queue<(float, float)> _smoothScalingQueue = new Queue<(float, float)>(SmoothScalingQueueSize);

        internal static int MaxPointsFromWidth(float width)
        {
            return Math.Min((int) width / 2, MaxPlotPoints);
        }

        protected override void RedrawCanvas(SKCanvas canvas, SKImageInfo info)
        {
            var plotRect = new SKRect(AxisValuesVisible ? TickBoxMargin : 0, 0, info.Width, info.Height);

            canvas.DrawRect(plotRect, FramePaint);

            //TODO: choose first x and last x instead?
            var xDiff = 0L;
            foreach (var line in _lines)
            {
                var viewer = line.Drawer.Viewer;
                // To achieve a constant line width, we need to scale the data when drawing the path, not scale the whole canvas
                xDiff = viewer.CurrentTimeRangeTo - viewer.CurrentTimeRangeFrom;
                if (xDiff == 0) continue;
                break;
            }

            if (xDiff == 0) return; // No data selected -> avoid divide-by-zero

            var earliestStartTime = _lines.Min(line => line.Drawer.Viewer.CurrentTimeRangeFrom);

            //TODO: make the percentage selectable
            var startX = earliestStartTime - xDiff * PreviewPercentage / 100;

            canvas.Save();
            canvas.ClipRect(plotRect);


            // TODO: fix this for SynchronizationContext by floating the line to the right
            if (startX < _context.AvailableTimeFrom && !(_context is SynchronizationContext))
            {
                startX = _context.AvailableTimeFrom;
            }

            // This keeps the current line at the same x-value independent of the visibility of the axes
            plotRect = new SKRect(0, 0, info.Width, info.Height);

            var scaleX = plotRect.Width / xDiff;

            ScaleLines(info, plotRect, out float? minYValue, out float? maxYValue, out bool allNumbAreInts);

            foreach (var line in _lines)
            {
                line.OffsetX = startX;
                line.ScaleX = scaleX;
            }

            if (CurrentTimeVisible)
            {
                // Draw current time axis
                var zeroX = ScalePointX(earliestStartTime, startX, scaleX);
                canvas.DrawLine(zeroX + plotRect.Left, plotRect.Top, zeroX + plotRect.Left, plotRect.Bottom, _currentLinePaint);
            }

            // TODO(sigurdal) this scales weirdly if frozen
            // Draw zero-x axis
            float zeroY;
            if (maxYValue.HasValue && minYValue.HasValue)
            {
                var scaleY = YScaleFromDiff(minYValue.Value, maxYValue.Value, info.Height);
                 zeroY = ScalePointY(0, maxYValue.Value, scaleY);
            } else
            {
                zeroY = ScalePointY(0, _lines.First().OffsetY, _lines.First().ScaleY);
            }
            canvas.DrawLine(plotRect.Left, zeroY, plotRect.Right, zeroY, _zeroLinePaint);

            foreach (var lineConfig in _lines)
            {
                DrawLine(canvas, plotRect, lineConfig);
            }

            DrawLegends(canvas, plotRect, _lines);

            if (!AxisValuesVisible || !minYValue.HasValue || !maxYValue.HasValue)
                return;

            _prevYValue = (minYValue, maxYValue);

            var axisValueRect = new SKRect(0, plotRect.Top, TickBoxMargin, plotRect.Bottom);
            canvas.Restore();
            canvas.ClipRect(axisValueRect);
            DrawTicks(canvas, axisValueRect, minYValue.Value, maxYValue.Value, allNumbAreInts);
        }

        private void ScaleLines(SKImageInfo info, SKRect plotRect, out float? minYValue, out float? maxYValue, out bool allNumbAreInts)
        {
            double windowWidth = plotRect.Width;
            double windowHight = plotRect.Height;

            minYValue = _minYValue;
            maxYValue = _maxYValue;
            allNumbAreInts = true;

            //Should only enter if, if scalingFrozen is true and window size has not changed
            if ((_scalingFrozen) && (windowWidth == _previouseWindowWidth) && (windowHight == _previouseWindowHeight))
            {
                minYValue = _prevYValue.minYValue;
                maxYValue = _prevYValue.maxYValue;
                allNumbAreInts = _previouseallNumbAreInts;
                return;
            }
            //Should only enter else if, if autoscale is true or if window size has changed
            else if ((AutoScale) || (windowWidth != _previouseWindowWidth) || (windowHight != _previouseWindowHeight))
            {
                if (!_autoScaleIndependent)
                {
                    var curMin = float.MaxValue;
                    var curMax = float.MinValue;
                    foreach (var line in _lines)
                    {
                        var (cMin, cMax, _allNumbAreInts) = line.Drawer.GetVisibleYStatistics(MaxPointsFromWidth(plotRect.Width));

                        // Do not include NaN or Inf
                        if (!Double.IsNaN(cMin) && !Double.IsInfinity(cMin))
                        {
                            curMin = Math.Min(curMin, cMin);
                        }
                        if (!Double.IsNaN(cMax) && !Double.IsInfinity(cMax))
                        {
                            curMax = Math.Max(curMax, cMax);
                        }
                        if (_allNumbAreInts == false)
                        { 
                            allNumbAreInts = _allNumbAreInts;
                        }
                    }
                    _previouseallNumbAreInts = allNumbAreInts;
                    if (_smoothScalingQueue.Count >= SmoothScalingQueueSize)
                    {
                        _smoothScalingQueue.Dequeue();
                    }

                    _smoothScalingQueue.Enqueue((curMin, curMax));

                    curMin = _smoothScalingQueue.Min(el => el.Item1);
                    curMax = _smoothScalingQueue.Max(el => el.Item2);

                    var yDelta = curMax - curMin;
                    if (yDelta <= 0)
                    {
                        yDelta = 1;
                        curMax -= yDelta / 2;
                    }

                    var scaleY = YScaleFromDiff(curMin, curMax, info.Height);
                    curMax -= PlotHeightMargin / scaleY;
                    foreach (var line in _lines)
                    {
                        line.OffsetY = curMax;
                        line.ScaleY = scaleY;
                    }

                    minYValue = curMin;
                    maxYValue = curMax;
                }
                else
                {
                    maxYValue = null;
                    foreach (var line in _lines)
                    {
                        var (cMin, cMax, _allNumbAreInts) = line.Drawer.GetVisibleYStatistics(MaxPointsFromWidth(plotRect.Width));
                        if (_allNumbAreInts == false)
                        { 
                            allNumbAreInts = _allNumbAreInts;
                        }
                        if (line.SmoothScalingQueue == null)
                        {
                            line.SmoothScalingQueue = new Queue<(float, float)>(SmoothScalingQueueSize);
                        }

                        if (line.SmoothScalingQueue.Count >= SmoothScalingQueueSize)
                        {
                            line.SmoothScalingQueue.Dequeue();
                        }

                        line.SmoothScalingQueue.Enqueue((cMin, cMax));

                        var curMin = line.SmoothScalingQueue.Min(el => el.Item1);
                        var curMax = line.SmoothScalingQueue.Max(el => el.Item2);

                        var scaleY = YScaleFromDiff(curMin, curMax, info.Height);
                        line.OffsetY = curMax - PlotHeightMargin / scaleY;
                        line.ScaleY = scaleY;
                    }
                    _previouseallNumbAreInts = allNumbAreInts;
                }
            }
            else
            {
                foreach (var line in _lines)
                {
                    line.ScaleY = -info.Height / (line.YDelta);
                    if (maxYValue.HasValue)
                        line.OffsetY = maxYValue.Value;
                }
            }

            _previouseWindowHeight = windowHight;
            _previouseWindowWidth = windowWidth;

        }

        private static float YScaleFromDiff(float yMin, float yMax, int height)
        {
            return -(height - PlotHeightMargin * 2) / (yMax - yMin);
        }

        private static float SmartRound(float num, float diff)
        {
            // This method should round to the nearest 1, 5, 10, 0.1 in a smart way
            // This could become very sophisticated, for example rounding to nearest 10 or 5 when proper
            if (diff < 0.01)
                return num;
            if (diff < 0.1)
                return (float)Math.Round(num, 2);
            if (diff < 5)
                return (float)Math.Round(num, 1);
            if (diff < 10)
                return (float)Math.Round(num*5, 1)/5;
            if (diff < 50)
                return (float)Math.Round(num, 0);
            if (diff < 100)
                return (float)Math.Round(num / 10, 0) * 10;

            //Since the num might be <50 we need to keap at leat 1 decimal or else the axis become 0
            return (float)Math.Round(num / 50, 1) * 50;
        }

        private static string GetFormat(float minY, float maxY, bool allNumbAreInts)
        {
            //Only use e if we have very big or very small numbers
            if ((Math.Abs(maxY) >= 100000) || (Math.Abs(minY) < 0.001))
            {
                return "0.0e0";
            }
            //Test if the data only consist of int, if both the min and the max of the dataset are ints
            //the dataset probably only consist of ints. The ticks on y axis will also be ints if the
            //largest number is above 5
            else if (allNumbAreInts || Math.Abs(maxY) > 5)
            {
 
                return "#####";
            }
            //If we have only numbers smaller then 5 and not all are ints, the ticks on the y axis 
            //will be a decimal number
            else
            {
                return "##.###";
            }

        }

        private void DrawTicks(SKCanvas canvas, SKRect drawRect, float minY, float maxY, bool allNumbAreInts)
        {
            var diffY = maxY - minY;
            var valueFormat = GetFormat(minY, maxY, allNumbAreInts);
            uint nTicks = 8;


            var tickStart = minY + (diffY / 2f);

            // If we cross the zero-axis, use zero as the tick center, if not round it smartly
            if (minY < 0 && 0 < maxY)
            { 
                tickStart = 0;
            }
            else
            { 
                tickStart = SmartRound(tickStart, diffY);
            }

            var tickDelta = SmartRound(diffY / nTicks, diffY);
            var scale = -drawRect.Height/diffY;

            

            for (var i = -nTicks; i < nTicks; i++)
            {
                var val = tickStart + i * tickDelta;
                var drawVal = ScalePointY(val, maxY, scale);
                var valueText = (val).ToString(valueFormat);
                if (valueText == "")
                {
                    continue;
                }
                var textSize = TickTextPaint.MeasureText(valueText);
                canvas.DrawText(valueText, TickBoxMargin - TickLength - TickMargin- textSize, drawVal, TickTextPaint);
                canvas.DrawLine(TickBoxMargin - TickLength, drawVal, TickBoxMargin, drawVal, _legendStroke);

            }

        }

        private void DrawLegends(SKCanvas canvas, SKRect drawRect, IReadOnlyCollection<LineConfiguration> configs)
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

        public void InvalidateSurface()
        {
            Canvas.InvalidateSurface();
        }

        protected const string RemoveLineText = "Remove Line";
        protected const string AutoScaleIndependentText = "AutoScale Independent";
        protected const string AutoScaleCommonText = "AutoScale Common";
        protected const string FreezeScalingText = "Freeze scaling";
        protected const string UnfreezeScalingText = "Unfreeze scaling";
        protected const string ScatterPlotText = "Scatter Plot";
        protected const string LinePlotText = "Line Plot";
        protected const string ColumnPlotText = "Column Plot";

        protected override bool GetExtraMenuParameters(List<string> parameters)
        {
            if (_lines.Count > 1) parameters.Add(RemoveLineText);

            parameters.Add(_autoScaleIndependent ? AutoScaleCommonText : AutoScaleIndependentText);
            parameters.Add(_scalingFrozen ? UnfreezeScalingText : FreezeScalingText);

            switch (PlotType)
            {
                case PlotTypes.Line:
                    parameters.Add(ScatterPlotText);
                    parameters.Add(ColumnPlotText);
                    break;
                case PlotTypes.Scatter:
                    parameters.Add(LinePlotText);
                    parameters.Add(ColumnPlotText);
                    break;
                case PlotTypes.Column:
                    parameters.Add(LinePlotText);
                    parameters.Add(ScatterPlotText);
                    break;
            }

            return true;
        }

        protected override async void OnHandleMenuResult(Page page, string action)
        {
            switch (action)
            {
                case AutoScaleCommonText:
                    _autoScaleIndependent = false;
                    InvalidateSurface();
                    return;
                case AutoScaleIndependentText:
                    _autoScaleIndependent = true;
                    InvalidateSurface();
                    return;
                case FreezeScalingText:
                    _scalingFrozen = true;
                    return;
                case UnfreezeScalingText:
                    _scalingFrozen = false;
                    return;
                case LinePlotText:
                    PlotType = PlotTypes.Line;
                    InvalidateSurface();
                    return;
                case ScatterPlotText:
                    PlotType = PlotTypes.Scatter;
                    InvalidateSurface();
                    return;
                case ColumnPlotText:
                    PlotType = PlotTypes.Column;
                    InvalidateSurface();
                    return;
                case RemoveLineText:
                    var lineToRemoveAction = await page.DisplayActionSheet("Remove Line", CancelText, null,
                        _lines.Select(line => line.Drawer.Legend).ToArray());
                    if (lineToRemoveAction == null || lineToRemoveAction == CancelText)
                        return;

                    RemoveLines(_lines.FindAll(line => line.Drawer.Legend == lineToRemoveAction));
                    return;
            }
            base.OnHandleMenuResult(page, action);
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
        private List<LineConfiguration> FindLines(IDataPoint datapoint)
        {
            return _lines.FindAll(lp => lp.Drawer.Viewer.DataPoint == datapoint);
        }


        /// Remove lines from plot, and remove plot if the last line is removed.
        private void RemoveLines(IReadOnlyCollection<LineConfiguration> linesToRemove)
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
    }

    public class LineConfiguration
    {
        public LineConfiguration(LinePlot linePlot)
        {
            _linePlot = linePlot;
        }
        public ILineDrawer Drawer { get; set; }

        public long OffsetX { get; set; }
        public float ScaleX { get; set; }
        public float YDelta { get; set; }

        public float OffsetY { get; set; }
        public float ScaleY { get; set; }
        public SKPaint LinePaint { get; set; }
        public PlotTypes PlotType { get => _linePlot.PlotType; }

        public Queue<(float, float)> SmoothScalingQueue;
        private LinePlot _linePlot;
    }

    public enum PlotTypes
    {
        Line, Scatter, Column
    }
}
