using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Pages.Synchronization;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Figures
{
    public class CorrelationPlot : DrawPlot
    {
        public static async Task<CorrelationPlot> Create(IDataPoint datapoint, TimeSynchronizedContext context, PointSynchronizationPage pointSyncPage)
        {
            var correlationPlot = new CorrelationPlot(context, datapoint, pointSyncPage);
            var lineDrawer = await correlationPlot.CreateLineDrawer(datapoint);
            correlationPlot.AddLine(lineDrawer);
            return correlationPlot;
        }

        private readonly PointSynchronizationPage _pointSyncPage;
        private CorrelationPlot(TimeSynchronizedContext context, IDataPoint dataPoint, PointSynchronizationPage pointSyncPage) : base(context, dataPoint)
        {
            this.Canvas.EnableTouchEvents = true;
            this.Canvas.Touch += OnTouch;
            this._pointSyncPage = pointSyncPage;
        }

        ~CorrelationPlot()
        {
            this.Canvas.Touch -= OnTouch;
        }

        private int _canvasWidth;
        private void OnTouch(object sender, SKTouchEventArgs e)
        {

            if (e.ActionType != SKTouchAction.Released)
            {
                return;
            }
            if (e.MouseButton == SKMouseButton.Right)
            {
                MenuButton_OnClicked(this, new EventArgs());
                return;
            }
            if(e.MouseButton != SKMouseButton.Left)
            {
                return;
            }

            var mouseClickLocationX = e.Location.X;
            var relativeMouseClickLocationX = mouseClickLocationX / _canvasWidth;

            ITimeSeriesViewer viewer = (ITimeSeriesViewer)Viewers[0];
            var span = viewer.GetCurrentData<float>();
            int lenTimeVec = span.X.Length;
            int relevantIndex = (int)(relativeMouseClickLocationX * lenTimeVec);
            long timeOffset = span.X.ToArray()[relevantIndex];
            double timeOffsetSec = -timeOffset / 1000000;
            _pointSyncPage.adjustOffset(this, new ValueChangedEventArgs(0, timeOffsetSec));

        }

        private bool _axisValuesVisible = true;
        public bool AxisValuesVisible
        {
            get => _axisValuesVisible;
            set => _axisValuesVisible = value;
        }

        protected override void RedrawCanvas(SKCanvas canvas, SKImageInfo info)
        {
            var plotRect = new SKRect(AxisValuesVisible ? TickBoxMargin : 0, 0, info.Width, info.Height);
            _canvasWidth = info.Width;
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
            autoscaleIndependet(info, plotRect, out float? minYValue, out float? maxYValue);

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
            }
            else
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

            var axisValueRect = new SKRect(0, plotRect.Top, TickBoxMargin, plotRect.Bottom);
            canvas.Restore();
            canvas.ClipRect(axisValueRect);
            //Draws the ticks of the y scale the min and max value is not for the data, but it is the data
            //pluss the marigns which are added in scale lines

        }

        /// <summary>
        /// All lines are scaled independely of each other, all though they are plottet in the same lineplot.
        /// This means that no y scale is shown since the scale is dependent on each line in lineplot
        /// The returned min and max is after the margins have been taken into consideration
        /// </summary>
        private void autoscaleIndependet(SKImageInfo info, SKRect plotRect, out float? minYValue, out float? maxYValue)
        {
            maxYValue = null;
            minYValue = null;


            foreach (var line in _lines)
            {
                var (cMin, cMax) = line.Drawer.GetVisibleYStatistics(0);

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
                line.ScaleY = scaleY;
            }
        }

        private void syncOnMaxValue()
        {
            ITimeSeriesViewer viewer = (ITimeSeriesViewer)Viewers[0];
            var span = viewer.GetCurrentData<float>();
            var valueArray = span.Y.ToArray().Select((index, value) => index);
            int maxIndex = valueArray.Select((value, index) => new { Value = value, Index = index }).Aggregate((a, b) => (a.Value > b.Value) ? a : b).Index;
            long timeOffset = span.X.ToArray()[maxIndex];
            float timeOffsetSec = (float)Math.Round(timeOffset / 1000000f, 2);
            _pointSyncPage.adjustOffset(this, new ValueChangedEventArgs(0, -timeOffsetSec));
        }


        private const string SyncBasedOnMax = "synchronize based on Max Correlation";

        protected override bool GetExtraMenuParameters(List<string> parameters)
        {
            parameters.Clear();
            parameters.Add(SyncBasedOnMax);
            return true;

        }

        protected override void OnHandleMenuResult(Page page, string action)
        {
            switch (action)
            {
                case SyncBasedOnMax:
                    syncOnMaxValue();
                    InvalidateSurface();
                    return;

            }
            base.OnHandleMenuResult(page, action);
        }

        protected override void RemoveThisView()
        {
            _pointSyncPage.removeCorrelationPreview(this.DataPoints[0]);
        }

    }
}