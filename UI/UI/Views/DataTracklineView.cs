﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Interfaces;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views
{
    [DesignTimeVisible(true)]
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public class DataTracklineView : SKCanvasView
    {

        private readonly List<(ITimeViewer, TimeSynchronizedContext, string)> _timeViewers = new List<(ITimeViewer, TimeSynchronizedContext, string)>();
        private readonly List<(IDataPoint, ITimeViewer, IDataViewer)> _dataTimeList = new List<(IDataPoint, ITimeViewer, IDataViewer)>();

        private readonly SKPaint _currentLinePaint = new SKPaint
        {
            Color = SKColors.Lime,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true
        };

        private void ContextOnSelectedTimeRangeChanged(SingleSetDataViewerContext sender, long @from, long to)
        {
            InvalidateSurface();
        }

        private void ViewerOnTimeChanged(ITimeViewer sender, long start, long end)
        {
            InvalidateSurface();
        }

        public DataTracklineView()
        {
            PaintSurface += OnPaintSurface;
            WidthMargins = 10;
        }

        public int WidthMargins { get; set; }

        protected void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;

            canvas.Clear(SKColors.White);

            var height = e.Info.Height;
            var width = e.Info.Width;

            var drawRect = new SKRect(0, 0, width, height);

            var borderPaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };
            drawRect.Right -= 1;
            drawRect.Bottom -= 1;

            canvas.DrawRect(drawRect, borderPaint);

            drawRect.Left = WidthMargins;
            drawRect.Right -= WidthMargins;
            drawRect.Top = 2;
            drawRect.Bottom -= 2;

            var trans = SKMatrix.MakeTranslation(drawRect.Left, drawRect.Top);
            canvas.SetMatrix(trans);

            var (xMin, xScale) = DrawDataSegments(e.Surface.Canvas, drawRect, _timeViewers);
            if (!_timeViewers.Any()) return;
            canvas.SetMatrix(SKMatrix.MakeIdentity());
            DrawCurrentTime(canvas, xMin, xScale);
        }

        private void DrawCurrentTime(SKCanvas canvas, long xMin, float xScale)
        {
            var timeViewerItem = _timeViewers.First();
            var currentTimePos = timeViewerItem.Item2.SelectedTimeFrom;

            var xPos = (currentTimePos - xMin) * xScale;
            canvas.DrawLine(xPos, 0, xPos, canvas.LocalClipBounds.Height, _currentLinePaint);
        }

        private static (List<(long, long, string)>, long, float) GetMinTimeAndScale(IReadOnlyCollection<(ITimeViewer, TimeSynchronizedContext, string)> timeViewers, SKRect drawRect)
        {
            var times = new List<(long, long, string)>();
            foreach (var (viewer, context, label) in timeViewers)
            {
                var (start, end) = context.GetAvailableTimeInContext(viewer);
                times.Add((start, end, label));
            }

            var xMin = times.Min(el => el.Item1);
            var xMax = times.Max(el => el.Item2);
            var xDiff = xMax - xMin;
            var xScale = drawRect.Width / xDiff;

            return (times, xMin, xScale);
        }

        private (long, float) DrawDataSegments(SKCanvas canvas, SKRect drawRect, IReadOnlyCollection<(ITimeViewer, TimeSynchronizedContext, string)> timeViewers)
        {
            if (timeViewers.Count == 0) return (0,0);

            const float labelXMargin = 4f;
            const float boxRoundnessX = 5f;
            const float boxRoundnessY = boxRoundnessX;
            const float minTextSize = 10f;
            const float yMargin = 2f;
            const float maxTrackHeight = 30f;

            var (times, xMin, xScale) = GetMinTimeAndScale(timeViewers, drawRect);

            var nLines = _timeViewers.Count;
            var yHeight = (drawRect.Height) / nLines - yMargin;

            if (yHeight > maxTrackHeight) yHeight = maxTrackHeight;


            var boldFont = SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, SKFontStyle.Bold);

            var paint = new SKPaint
            {
                Color = SKColors.Red,
                StrokeWidth = 1,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            var textPaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                SubpixelText = true,
                Typeface = boldFont
            };

            var fontHeight = textPaint.FontMetrics.Bottom - textPaint.FontMetrics.Top;
            var textSize = textPaint.TextSize;
            if (yHeight < Math.Abs(fontHeight))
            {
                textSize = Math.Abs(yHeight / fontHeight * textPaint.TextSize);
            }

            textPaint.TextSize = Math.Max(textSize, minTextSize);

            var fontBottom =
                Math.Abs(textPaint.FontMetrics.Bottom - textPaint.FontMetrics.Top) < yHeight
                    ? yHeight / 2 - textPaint.FontMetrics.Bottom
                    : 0f;

            var yPos = yMargin;
            foreach (var (start, end, label) in times)
            {
                var xPos = (start - xMin) * xScale;
                var width = Math.Max((end - start) * xScale, 1f);
                canvas.DrawRoundRect(xPos, yPos, width, yHeight, boxRoundnessX, boxRoundnessY, paint);
                canvas.DrawText(label, xPos + labelXMargin, yPos + yHeight - fontBottom, textPaint);

                yPos += yHeight + yMargin;
            }

            return (xMin, xScale);
        }

        private void ContextOnAvailableTimeRangeChanged(DataViewerContext sender, long @from, long to)
        {
            InvalidateSurface();
        }

        public async Task AddDataPoint(IDataPoint dataPoint, TimeSynchronizedContext context)
        {
            if (!_dataTimeList.Any())
            {
                context.AvailableTimeRangeChanged += ContextOnAvailableTimeRangeChanged;
            }

            var dataViewer = await context.GetDataViewerFor(dataPoint);
            var timeViewer = await dataViewer.DataPoint.Time.CreateViewer();
            _dataTimeList.Add((dataPoint, timeViewer, dataViewer));
            _timeViewers.Add((timeViewer, context, dataPoint.Name));
            timeViewer.TimeChanged += ViewerOnTimeChanged;
            context.SelectedTimeRangeChanged += ContextOnSelectedTimeRangeChanged;
            InvalidateSurface();
        }

        public void RemoveDataPoint(IDataPoint dataPoint, TimeSynchronizedContext context)
        {
            var found = false;
            int index;
            for (index = 0; index < _dataTimeList.Count; index++)
            {
                if (_dataTimeList[index].Item1 != dataPoint) continue;
                if (_timeViewers[index].Item2 != context) continue;

                found = true;
                break;
            }

            if (!found)
                return;

            var(_, timeViewer, dataViewer) = _dataTimeList[index];
            _dataTimeList.RemoveAt(index);
            if (_dataTimeList.Count == 0)
            {
                context.AvailableTimeRangeChanged -= ContextOnAvailableTimeRangeChanged;
            }

            timeViewer.TimeChanged -= ViewerOnTimeChanged;
            context.Remove(dataViewer);
            context.SelectedTimeRangeChanged -= ContextOnSelectedTimeRangeChanged;
            _timeViewers.RemoveAt(index);

            InvalidateSurface();
        }

        private async void DataPointAddedHandler(object sender, (IDataPoint, DataViewerContext) args)
        {
            var (datapoint, context) = args;
            if (context is TimeSynchronizedContext timeContext)
            {
                try
                {
                    await AddDataPoint(datapoint, timeContext);
                }
                catch (Exception ex)
                {
                    await XamarinHelpers.ShowErrorMessage("Error", ex.Message, XamarinHelpers.GetCurrentPage(Navigation));
                }
            }
        }
        private void DataPointRemovedHandler(object sender, (IDataPoint, DataViewerContext) args)
        {
            var (datapoint, context) = args;
            if (context is TimeSynchronizedContext timeContext)
                RemoveDataPoint(datapoint, timeContext);
        }

        public void RegisterFigureContainer(IFigureContainer container)
        {
            container.DatapointAdded += DataPointAddedHandler;
            container.DatapointRemoved += DataPointRemovedHandler;
        }

        public void DeregisterFigureContainer(IFigureContainer container)
        {
            container.DatapointAdded -= DataPointAddedHandler;
            container.DatapointRemoved -= DataPointRemovedHandler;
        }
    }
}