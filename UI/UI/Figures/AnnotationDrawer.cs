using SINTEF.AutoActive.Plugins.Import.Json;
using SINTEF.AutoActive.UI.Figures.LinePaintProviders;
using SkiaSharp;
using System;
using System.Collections.Generic;
using SINTEF.AutoActive.Databus.Common;

namespace SINTEF.AutoActive.UI.Figures
{
    public class AnnotationDrawer<T> : LineDrawer<T> where T : IConvertible
    {
        public AnnotationDrawer(ITimeSeriesViewer viewer) : base(viewer)
        {
        }

        public bool CircularMarker = true;

        public override bool IsAnnotation => true;

        private readonly Dictionary<int, SKPaint> _paints = new Dictionary<int, SKPaint>();
        private readonly MatPlotLib2LinePaint _paintProvider = new MatPlotLib2LinePaint(2, 3);

        public override void DrawPath(SKCanvas canvas, SKRect drawRect, LineConfiguration lineConfig)
        {
            var data = Viewer.GetCurrentData<int>();

            var height = Math.Max(20, drawRect.Height / 20);
            var width = 20;

            var dataIsSorted = false;

            var paths = new Dictionary<int, SKPath>();

            // Generate cirle-paths for all points
            for (var i = 0; i < data.X.Length; i++)
            {
                var y = data.Y[i];
                if (!paths.TryGetValue(y, out SKPath plot))
                {
                    paths.Add(y, plot = new SKPath());
                }

                var plotX = DrawPlot.ScalePointX(data.X[i], lineConfig.OffsetX, lineConfig.ScaleX);
                if (dataIsSorted && plotX > drawRect.Width)
                {
                    break;
                }

                var x = plotX + drawRect.Left;
                if (CircularMarker)
                    plot.AddCircle(x, drawRect.Bottom, height);
                else
                    plot.AddRect(new SKRect(x - width / 2f, drawRect.Bottom - height, x + width / 2f, drawRect.Bottom));
            }

            // Draw the paths
            foreach (var path in paths)
            {
                if (!_paints.TryGetValue(path.Key, out var paint))
                {
                    _paints.Add(path.Key, paint = _paintProvider.GetIndexedPaint(path.Key));
                    paint.Style = SKPaintStyle.StrokeAndFill;
                    paint.Color = paint.Color.WithAlpha(0x7f);
                }
                canvas.DrawPath(path.Value, paint);
            }

            var centerPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeJoin = SKStrokeJoin.Miter,
                IsAntialias = true,
                Color = new SKColor(0, 0, 0),
                TextSize = 16,
            };

            // Draw center line
            foreach (var x in data.X)
            {
                var plotX = DrawPlot.ScalePointX(x, lineConfig.OffsetX, lineConfig.ScaleX);
                if (dataIsSorted && plotX > drawRect.Width)
                {
                    break;
                }

                var bottomPoint = new SKPoint(plotX, drawRect.Bottom);
                var topPoint = new SKPoint(plotX, drawRect.Bottom - height / 2);
                canvas.DrawLine(topPoint, bottomPoint, centerPaint);
            }

            // Check if we should draw annotation tags
            if (!(Viewer is AnnotationDataViewer annotationViewer))
            {
                return;
            }

            var annotationSet = annotationViewer.AnnotationSet;
            if (annotationSet.AnnotationInfo == null || annotationSet.AnnotationInfo.Count == 0)
            {
                return;
            }

            var textMargin = 3;

            foreach (var annotation in annotationSet.Annotations)
            {
                var plotX = DrawPlot.ScalePointX(annotation.Timestamp, lineConfig.OffsetX, lineConfig.ScaleX);
                if (dataIsSorted && plotX > drawRect.Width)
                {
                    break;
                }

                if (!annotationSet.AnnotationInfo.TryGetValue(annotation.Type, out var info))
                    continue;
                if (info.Tag == null)
                    continue;

                var topPoint = new SKPoint(plotX - centerPaint.MeasureText(info.Tag) / 2, textMargin + drawRect.Bottom - height / 2);
                canvas.DrawText(info.Tag, topPoint, centerPaint);
            }
        }
    }
}
