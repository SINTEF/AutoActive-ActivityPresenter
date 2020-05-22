using System;
using SINTEF.AutoActive.Databus.Common;
using SkiaSharp;

namespace SINTEF.AutoActive.UI.Figures
{
    public interface ILineDrawer
    {
        void CreatePath(SKPath plot, SKRect drawRect, LineConfiguration lineConfig);

        float MinY { get; }
        float MaxY { get; }

        (float, float, bool) GetVisibleYStatistics(int maxPoints);

        ITimeSeriesViewer Viewer { get; }
        LinePlot Parent { get; set; }

        string Legend { get; set; }
    }


    public class LineDrawer<T> : ILineDrawer where T : IConvertible
    {
        public LinePlot Parent { get; set; }
        public string Legend { get; set; }

        public ITimeSeriesViewer Viewer { get; }
        public LineDrawer(ITimeSeriesViewer viewer)
        {
            Viewer = viewer;
            if (viewer.MinValueHint.HasValue) _minYValue = (float)viewer.MinValueHint.Value;
            if (viewer.MaxValueHint.HasValue) _maxYValue = (float)viewer.MaxValueHint.Value;
        }

        public void CreatePath(SKPath plot, SKRect drawRect, LineConfiguration lineConfig)
        {
            var en = Viewer.GetCurrentData<T>().GetEnumerator(LinePlot.MaxPointsFromWidth(drawRect.Width));

            switch (lineConfig.PlotType)
            {
                case PlotTypes.Line:
                    DrawLine(plot, drawRect, lineConfig, en);
                    break;
                case PlotTypes.Scatter:
                    DrawScatter(plot, drawRect, lineConfig, en);
                    break;
                case PlotTypes.Column:
                    DrawColumn(plot, drawRect, lineConfig, en);
                    break;
            }
        }

        private static void DrawColumn(SKPath plot, SKRect drawRect, LineConfiguration lineConfig, SpanPair<T>.Enumerator en)
        {
            if (!en.MoveNext()) return;

            var startX = Math.Max(LinePlot.ScalePointX(en.Current.x, lineConfig.OffsetX, lineConfig.ScaleX), 0);
            var startY = LinePlot.ScalePointY(Convert.ToSingle(en.Current.y), lineConfig.OffsetY, lineConfig.ScaleY);
            plot.MoveTo(startX + drawRect.Left, startY);
            var prevY = startY;
            while (en.MoveNext())
            {
                var plotX = LinePlot.ScalePointX(en.Current.x, lineConfig.OffsetX, lineConfig.ScaleX);

                if (plotX > drawRect.Width)
                {
                    plot.LineTo(drawRect.Right, prevY);
                    break;
                }

                var valY = LinePlot.ScalePointY(Convert.ToSingle(en.Current.y), lineConfig.OffsetY, lineConfig.ScaleY);
                plot.LineTo(plotX + drawRect.Left, prevY);
                plot.LineTo(plotX + drawRect.Left, valY);
                prevY = valY;
            }
        }

        private static void DrawScatter(SKPath plot, SKRect drawRect, LineConfiguration lineConfig, SpanPair<T>.Enumerator en)
        {
            while (en.MoveNext())
            {
                if(en.Current.isNan) continue;
                var plotX = LinePlot.ScalePointX(en.Current.x, lineConfig.OffsetX, lineConfig.ScaleX);
                if (plotX > drawRect.Width)
                {
                    break;
                }

                var valY = LinePlot.ScalePointY(Convert.ToSingle(en.Current.y), lineConfig.OffsetY, lineConfig.ScaleY);

                plot.AddCircle(plotX + drawRect.Left, valY, 1);
            }
        }

        private static void DrawLine(SKPath plot, SKRect drawRect, LineConfiguration lineConfig, SpanPair<T>.Enumerator en)
        {

            if (!en.MoveNext()) return;
            
            var startX = Math.Max(LinePlot.ScalePointX(en.Current.x, lineConfig.OffsetX, lineConfig.ScaleX), 0);
            plot.MoveTo(startX + drawRect.Left, LinePlot.ScalePointY(Convert.ToSingle(en.Current.y), lineConfig.OffsetY, lineConfig.ScaleY));
            while (en.MoveNext())
            {
                if (en.Current.isNan) continue;
                var plotX = LinePlot.ScalePointX(en.Current.x, lineConfig.OffsetX, lineConfig.ScaleX);
                if (plotX > drawRect.Width)
                {
                    break;
                }

                var valY = LinePlot.ScalePointY(Convert.ToSingle(en.Current.y), lineConfig.OffsetY, lineConfig.ScaleY);
                plot.LineTo(plotX + drawRect.Left, valY);
            }
        }

        // ---- Scaling ----
        private float _minYValue = -1;
        private float _maxYValue = 1;

        public float MinY
        {
            get => _minYValue;
            set
            {
                if (!Double.IsNaN(value))
                    {
                    _minYValue = value;
                    }
                Parent?.InvalidateSurface();
            }
        }

        public float MaxY
        {
            get => _maxYValue;
            set
            {
                if (!Double.IsNaN(value))
                {
                    _maxYValue = value;
                }
                Parent?.InvalidateSurface();
            }
        }



        public (float,float,bool) GetVisibleYStatistics(int maxPoints)
        {
            var en = Viewer.GetCurrentData<T>().GetEnumerator(maxPoints);
            var (min, max) = (float.MaxValue, float.MinValue);
            bool allNumbAreInts = true;
            while (en.MoveNext())
            {
                var el = Convert.ToSingle(en.Current.y);
                min = Math.Min(el, min);
                max = Math.Max(el, max);
                if (el != (int)el) allNumbAreInts = false;

            }

            return (min, max, allNumbAreInts);
        }
    }
}
