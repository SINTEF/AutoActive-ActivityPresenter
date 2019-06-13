﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms.Xaml;

namespace SINTEF.AutoActive.UI.Views
{
    [DesignTimeVisible(true)]
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public class DataTracklineView : SKCanvasView
    {

        private readonly List<(ITimeViewer, string)> _timeViewers = new List<(ITimeViewer, string)>();
        private readonly Dictionary<IDataPoint, ITimeViewer> _dataTimeDict = new Dictionary<IDataPoint, ITimeViewer>();

        public void AddTimeViewer(ITimeViewer viewer, string label)
        {
            _timeViewers.Add((viewer, label));
            InvalidateSurface();
        }

        private void RemoveTimeViewer(ITimeViewer timeViewer)
        {
            _timeViewers.RemoveAll(el => el.Item1 == timeViewer);
        }

        public DataTracklineView()
        {
            PaintSurface += OnPaintSurface;
        }

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

            drawRect.Left = 10;
            drawRect.Right -= 10;
            drawRect.Top = 2;
            drawRect.Bottom -= 2;

            var trans = SKMatrix.MakeTranslation(drawRect.Left, drawRect.Top);
            canvas.SetMatrix(trans);

            DrawDataSegments(e.Surface.Canvas, drawRect, _timeViewers);
        }

        private void DrawDataSegments(SKCanvas canvas, SKRect drawRect, IReadOnlyCollection<(ITimeViewer, string)> timeViewers)
        {
            if (timeViewers.Count == 0) return;

            const float labelXMargin = 4f;
            const float boxRoundnessX = 5f;
            const float boxRoundnessY = boxRoundnessX;
            const float minTextSize = 10f;
            const float yMargin = 2f;


            var xMin = timeViewers.Min(el => el.Item1.Start);
            var xMax = timeViewers.Max(el => el.Item1.End);
            var xDiff = xMax - xMin;
            var xScale = drawRect.Width / xDiff;

            var nLines = _timeViewers.Count;
            var yHeight = (drawRect.Height) / nLines - yMargin;

            var paint = new SKPaint
            {
                Color = SKColors.Red,
                StrokeWidth = 1,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            var boldFont = SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, SKFontStyle.Bold);

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
            foreach (var (viewer, label) in timeViewers)
            {
                var xPos = (viewer.Start - xMin) * xScale;
                var width = (viewer.End - viewer.Start) * xScale;
                canvas.DrawRoundRect(xPos, yPos, width, yHeight, boxRoundnessX, boxRoundnessY, paint);
                canvas.DrawText(label, xPos + labelXMargin, yPos + yHeight - fontBottom, textPaint);

                yPos += yHeight + yMargin;
            }
        }

        public async Task AddDataPoint(IDataPoint dataPoint, TimeSynchronizedContext context)
        {
            if (_dataTimeDict.Count == 0)
            {
                context.AvailableTimeRangeChanged += ContextOnAvailableTimeRangeChanged;
            }
            if (_dataTimeDict.ContainsKey(dataPoint))
            {
                return;
            }

            var dataViewer = await context.GetDataViewerFor(dataPoint);
            var timeViewer = await dataViewer.DataPoint.Time.CreateViewer();
            _dataTimeDict[dataPoint] = timeViewer;
            AddTimeViewer(timeViewer, dataPoint.Name);
            timeViewer.TimeChanged += TimeViewer_TimeChanged;
        }

        private void ContextOnAvailableTimeRangeChanged(DataViewerContext sender, long @from, long to)
        {
            InvalidateSurface();
        }

        private void TimeViewer_TimeChanged(ITimeViewer sender, long start, long end)
        {
            InvalidateSurface();
        }

        public async Task RemoveDataPoint(IDataPoint dataPoint, TimeSynchronizedContext context)
        {
            if (!_dataTimeDict.TryGetValue(dataPoint, out var timeViewer))
            {
                //TODO: add warning
                return;
            }
            _dataTimeDict.Remove(dataPoint);
            if (_dataTimeDict.Count == 0)
            {
                context.AvailableTimeRangeChanged -= ContextOnAvailableTimeRangeChanged;
            }

            timeViewer.TimeChanged -= TimeViewer_TimeChanged;
            RemoveTimeViewer(timeViewer);
        }
    }
}