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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using SINTEF.AutoActive.UI.Interfaces;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Figures
{
    public class LinePlot : DrawPlot
    {

        public static async Task<LinePlot> Create(IDataPoint datapoint, TimeSynchronizedContext context)
        {
            var linePlot = new LinePlot(context, datapoint);

            var lineDrawer = await linePlot.CreateLineDrawer(datapoint);
            linePlot.AddLine(lineDrawer);

            return linePlot;
        }

        protected LinePlot(TimeSynchronizedContext context, IDataPoint dataPoint) : base(context, dataPoint)
        {
        }

        private double _previouseWindowHeight = 0;
        private double _previouseWindowWidth = 0;
        private bool _showOnlyInts = false;
        private bool _autoScaleIndependent;
        private bool _scalingFrozen;
        private (float? minYValue, float? maxYValue) _prevYValue;
        private bool _axisValuesVisible = true;
        public bool AxisValuesVisible
        {
            get => _axisValuesVisible && !_autoScaleIndependent;
            set => _axisValuesVisible = value;
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

            //Takes thes margins of the plot into consideration when calculating min
            //and max values
            ScaleLines(info, plotRect, out float? minYValue, out float? maxYValue);

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

            float zeroY;
            if (maxYValue.HasValue && minYValue.HasValue)
            {
                float scaleY = -info.Height / (maxYValue.Value - minYValue.Value); //calculate the scale, 1 data value equals x pixels
                zeroY = ScalePointY(0, maxYValue.Value, scaleY); //Where is 0 localized, if it is localized between 0 and height it is seen on screen. Zero is top of screen
            } else
            {
                zeroY = ScalePointY(0, _lines.First().OffsetY, _lines.First().ScaleY);
            }
            canvas.DrawLine(plotRect.Left, zeroY, plotRect.Right, zeroY, _zeroLinePaint); //Draws 0 line

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
            //Draws the ticks of the y scale the min and max value is not for the data, but it is the data
            //pluss the marigns which are added in scale lines
            DrawTicks(canvas, axisValueRect, minYValue.Value, maxYValue.Value);

        }

        /// <summary>
        /// Scales the lines in lineplot accordingly to the min and max of data and the plot margins
        /// The returned min and max is after the margins have been taken into consideration
        /// </summary>
        private void ScaleLines(SKImageInfo info, SKRect plotRect, out float? minYValue, out float? maxYValue)
        {

            //Should only enter if, if scalingFrozen is true and window size has not changed
            if ((_scalingFrozen) && (plotRect.Width == _previouseWindowWidth) && (plotRect.Height == _previouseWindowHeight))
            {
                frozenScaling(out minYValue, out maxYValue);
            }
            else if ((_scalingFrozen) && ((plotRect.Width != _previouseWindowWidth) || (plotRect.Height != _previouseWindowHeight)))
            {
                //in cases where scales are frozen but size of figure is updated these attributes should stay the same,
                //therefore is frozenScaling run after autoscale
                autoscale(info, plotRect, out minYValue, out maxYValue, true);
                frozenScaling(out minYValue, out maxYValue);
                _previouseWindowHeight = plotRect.Height;
                _previouseWindowWidth = plotRect.Width;
            }
            else
            {
                autoscale(info, plotRect, out minYValue, out maxYValue, false);
                _previouseWindowHeight = plotRect.Height;
                _previouseWindowWidth = plotRect.Width;
            }



        }
        /// <summary>
        /// Sets the scale equal to the previouse iteration
        /// The returned min and max is after the margins have been taken into consideration
        /// </summary>
        private void frozenScaling(out float? minYValue, out float? maxYValue)
        {
            minYValue = _prevYValue.minYValue; //I dont think this is correct, as _prevYValue = ()
            maxYValue = _prevYValue.maxYValue;
        }

        /// <summary>
        /// Scales the lines in plot according to the available data
        /// The returned min and max is after the margins have been taken into consideration
        /// </summary>

        private void autoscale(SKImageInfo info, SKRect plotRect, out float? minYValue, out float? maxYValue, bool resize)
        {
            if (!_autoScaleIndependent)
            {
                autoscaleDependent(info, plotRect, out minYValue, out maxYValue, resize);
            }
            else
            {
                autoscaleIndependet(info,  plotRect, out minYValue, out maxYValue, resize);
            }
        }

        /// <summary>
        /// Scales all lines dependently. uses the global min and max to scale all the lines if multiple
        /// lines are plottet in one lineplot.
        /// The returned min and max is after the margins have been taken into consideration
        /// </summary>
        private void autoscaleDependent(SKImageInfo info, SKRect plotRect, out float? minYValue, out float? maxYValue, bool resize)
        {
            minYValue = _minYValue;
            maxYValue = _maxYValue;


            var curMin = float.MaxValue;
            var curMax = float.MinValue;

            foreach (var line in _lines)
            {
                var (cMin, cMax) = line.Drawer.GetVisibleYStatistics(MaxPointsFromWidth(plotRect.Width));

                // Do not include NaN or Inf
                if (!Double.IsNaN(cMin) && !Double.IsInfinity(cMin))
                {
                    curMin = Math.Min(curMin, cMin);
                }
                if (!Double.IsNaN(cMax) && !Double.IsInfinity(cMax))
                {
                    curMax = Math.Max(curMax, cMax);
                }

            }
            if (_smoothScalingQueue.Count >= SmoothScalingQueueSize)
            {
                _smoothScalingQueue.Dequeue();
            }

            _smoothScalingQueue.Enqueue((curMin, curMax));

            curMin = _smoothScalingQueue.Min(el => el.Item1);
            curMax = _smoothScalingQueue.Max(el => el.Item2);

            //This is done to prevent tickdelta to become 0 and scale to become inf in DrawTicks()
            var yDelta = curMax - curMin;
            if (yDelta <= 0)
            {
                yDelta = 2f;
                curMax += yDelta / 2;
                curMin -= yDelta / 2;
            }

            var scaleY = YScaleFromDiff(curMin, curMax, info.Height); //The margins are subtracted in the function
            curMin += PlotHeightMargin / scaleY;
            curMax -= PlotHeightMargin / scaleY;
            foreach (var line in _lines)
            {
                //Should not be updated if scales are freezed and size of figure is updated at the same time
                if (!resize)
                {
                    line.OffsetY = curMax;
                }
                line.ScaleY = scaleY;
            }

            minYValue = curMin;
            maxYValue = curMax;
        }

        /// <summary>
        /// All lines are scaled independely of each other, all though they are plottet in the same lineplot.
        /// This means that no y scale is shown since the scale is dependent on each line in lineplot
        /// The returned min and max is after the margins have been taken into consideration
        /// </summary>
        private void autoscaleIndependet(SKImageInfo info, SKRect plotRect, out float? minYValue, out float? maxYValue, bool resize)
        {
            maxYValue = null;
            minYValue = null;


            foreach (var line in _lines)
            {
                var (cMin, cMax) = line.Drawer.GetVisibleYStatistics(MaxPointsFromWidth(plotRect.Width));

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
                //Should not be updated if scales are freezed and size of figure is updated at the same time
                if (!resize)
                {
                    line.OffsetY = curMax - PlotHeightMargin / scaleY;
                }
                line.ScaleY = scaleY;
            }
        }

        /// <summary>
        /// Calculates the scale between the data and pixels. Be aware that the marigns are subtracted,
        /// Finds the best resolution of the y scale.
        /// </summary>
        private static float SmartRound(float num, float diff)
        {
            // This method should round to the nearest 1, 5, 10, 0.1 in a smart way
            // This could become very sophisticated, for example rounding to nearest 10 or 5 when proper
            if (diff < 0.01)
                return num;
            if (diff < 0.1)
                return (float)Math.Round(num, 3);
            if (diff < 5)
                return (float)Math.Round(num, 2);
            if (diff < 10)
                return (float)Math.Round(num, 1);

            return (float)Math.Round(num, 0);

        }

        /// <summary>
        /// Finds the best format of the nr in y scale. Be aware that "##.###" can be ints, if difference
        /// between min and bax is >10. The min and the max is after the margins are taken into consideration.
        /// </summary>
        private static string GetFormat(float minY, float maxY)
        {
            var diffY = maxY - minY;
            //Only use e if we have very big or very small numbers
            if ((Math.Abs(maxY) >= 100000) || (Math.Abs(maxY) < 0.001))
            {
                return "0.0e0";
            }
            else
            {
                return "##.###";
            }

        }

        /// <summary>
        /// Draws the ticks of the y scale
        /// The min and the max is after the margins are taken into consideration.
        /// </summary>
        private void DrawTicks(SKCanvas canvas, SKRect drawRect, float minY, float maxY)
        {
            // If the difference is large enough we should only show ints, or should we just make it as a tick option?
            if(_showOnlyInts)
            {
                DrawIntTicks(canvas, drawRect, minY, maxY);
            }
            else
            {
                DrawOptimalTicks(canvas, drawRect, minY, maxY);
            }
        }

        /// <summary>
        ///  Draws the y ticks if the ticks are forced to be ints.
        ///  The min and the max is after the margins are taken into consideration.
        /// </summary>
        private void DrawIntTicks(SKCanvas canvas, SKRect drawRect, float minY, float maxY)
        {
            //This is necessary because have added margins to the min and max values
            int maxValue = (int)Math.Floor(maxY);
            int minValue = (int)Math.Ceiling(minY);

            int diffMinMiax = maxValue - minValue;
            var diffY = maxY - minY;
            int tickDelta = 1;
            const int maxTicks = 8;
            float scale = (float) (-drawRect.Height /diffY);

            //If the difference between the min and max value is too large we can not have a tick for every value
            if (diffMinMiax > maxTicks)
            {
                tickDelta = (int)Math.Ceiling((float)diffY / (maxTicks));
            }

            List<int> ticks = new List<int>();
            for (int i = minValue; i <= maxValue; i += tickDelta)
            {
                ticks.Add(i);
            }

            //Zero centers the ticks if we have both positive and negative numbers and not zero as a tick
            if ((minValue < 0) && (0 < maxValue) && (!ticks.Contains(0)))
            {

                int maxNegativeNumber = ticks.Select(i => i).Where(i => i < 0).ToList().Max();
                int maxPositiveNumber = ticks.Select(i => i).Where(i => i > 0).ToList().Max();

                var offset = Math.Min(Math.Abs(maxNegativeNumber), maxPositiveNumber);

                if (maxPositiveNumber == -offset)
                {
                    offset = -offset;
                }

                ticks = ticks.Select(i => i + offset).ToList();

            }

            foreach (int val in ticks)
            {
                var drawVal = ScalePointY(val, maxY, scale);
                var valueText = val.ToString();
                var textSize = TickTextPaint.MeasureText(valueText);
                canvas.DrawText(valueText, TickBoxMargin - TickLength - TickMargin - textSize, drawVal, TickTextPaint);
                canvas.DrawLine(TickBoxMargin - TickLength, drawVal, TickBoxMargin, drawVal, _legendStroke);

            }

        }

        /// <summary>
        /// Searches for the optimal resolution and format of y scale and draws the ticks accordlingly.
        /// The min and the max is after the margins are taken into consideration.
        /// </summary>
        private void DrawOptimalTicks(SKCanvas canvas, SKRect drawRect, float minY, float maxY)
        {
            var diffY = maxY - minY;
            var valueFormat = GetFormat(minY, maxY);
            uint nTicks = 8;

            float tickStart = minY + (diffY / 2f);

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


            var scale = -drawRect.Height / diffY;

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


        protected const string RemoveLineText = "Remove Line";
        protected const string AutoScaleIndependentText = "AutoScale Independent";
        protected const string AutoScaleCommonText = "AutoScale Common";
        protected const string FreezeScalingText = "Freeze scaling";
        protected const string UnfreezeScalingText = "Unfreeze scaling";
        protected const string ScatterPlotText = "Scatter Plot";
        protected const string LinePlotText = "Line Plot";
        protected const string ColumnPlotText = "Column Plot";
        protected const string ScaleShowOnlyInts = "Force Y-Scale to show whole numbers";
        protected const string ScaleSearchForResolution = "Find optimal Y-scale resoltuion";

        protected override bool GetExtraMenuParameters(List<string> parameters)
        {
            if (_lines.Count > 1) parameters.Add(RemoveLineText);

            parameters.Add(_autoScaleIndependent ? AutoScaleCommonText : AutoScaleIndependentText);
            parameters.Add(_scalingFrozen ? UnfreezeScalingText : FreezeScalingText);
            parameters.Add(_showOnlyInts ? ScaleSearchForResolution : ScaleShowOnlyInts);

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
                case ScaleShowOnlyInts:
                    _showOnlyInts = true;
                    InvalidateSurface();
                    return;
                case ScaleSearchForResolution:
                    _showOnlyInts = false;
                    InvalidateSurface();
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

        public void DeserializeParameters(JObject root)
        {
            _autoScaleIndependent = root["autoscale_independent"].Value<bool>();
            _scalingFrozen = root["scaling_frozen"].Value<bool>();
            _showOnlyInts = root["show_only_ints"].Value<bool>();
            _minYValue = root["min_y_value"].Value<float?>();
            _maxYValue = root["max_y_value"].Value<float?>();
            _prevYValue.minYValue = root["prev_min_y_value"].Value<float?>();
            _prevYValue.maxYValue = root["prev_max_y_value"].Value<float?>();

            PlotType = JsonConvert.DeserializeObject<PlotTypes>(root["plot_type"].Value<string>());

            var nLines = _lines.Count;

            var lineYOffsets = root["line_offset_ys"].Values<float>();
            var lineScaleYs = root["line_scale_ys"].Values<float>();

            foreach (var (line, (yOffset, scaleY)) in _lines.Zip(lineYOffsets.Zip(lineScaleYs, Tuple.Create), Tuple.Create))
            {
                line.OffsetY = yOffset;
                line.ScaleY = scaleY;
            }

            InvalidateSurface();
        }

        public JObject SerializeParameters()
        {
            var root = SerializableViewHelper.SerializeDefaults(null, this);

            root["autoscale_independent"] = _autoScaleIndependent;
            root["scaling_frozen"] = _scalingFrozen;
            root["show_only_ints"] = _showOnlyInts;
            root["min_y_value"] = _minYValue;
            root["max_y_value"] = _maxYValue;
            root["prev_min_y_value"] = _prevYValue.minYValue;
            root["prev_max_y_value"] = _prevYValue.maxYValue;

            root["line_offset_ys"] = new JArray(_lines.Select(line => line.OffsetY));
            root["line_scale_ys"] = new JArray(_lines.Select(line => line.ScaleY));

            root["plot_type"] = JsonConvert.SerializeObject(PlotType);

            return root;
        }
    }
}
