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

        (float, float) GetVisibleYMinMax();

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
            var offsetX = lineConfig.OffsetX;
            var scaleX = lineConfig.ScaleX;
            var offsetY = lineConfig.OffsetY;
            var scaleY = lineConfig.ScaleY;

            var en = Viewer.GetCurrentData<T>().GetEnumerator(MaxItems);

            if (!en.MoveNext()) return;

            var width = drawRect.Width;

            var startX = Math.Max(LinePlot.ScaleX(en.Current.x, offsetX, scaleX), 0);
            plot.MoveTo(startX + drawRect.Left, LinePlot.ScaleY(Convert.ToSingle(en.Current.y), offsetY, scaleY));
            var done = false;
            while (en.MoveNext() && !done)
            {
                var plotX = LinePlot.ScaleX(en.Current.x, offsetX, scaleX);
                if (plotX > width)
                {
                    plotX = width;
                    done = true;
                }

                var valY = LinePlot.ScaleY(Convert.ToSingle(en.Current.y), offsetY, scaleY);
                plot.LineTo(plotX + drawRect.Left, valY);
            }
        }

        public int MaxItems = LinePlot.MaxPlotPoints;
        // ---- Scaling ----
        private float _minYValue = -1;
        private float _maxYValue = 1;

        public float MinY
        {
            get => _minYValue;
            set
            {
                _minYValue = value;
                Parent?.InvalidateSurface();
            }
        }

        public float MaxY
        {
            get => _maxYValue;
            set
            {
                _maxYValue = value;
                Parent?.InvalidateSurface();
            }
        }



        public (float,float) GetVisibleYMinMax()
        {
            var en = Viewer.GetCurrentData<T>().GetEnumerator(MaxItems*10);
            var (min, max) = (float.MaxValue, float.MinValue);
            while (en.MoveNext())
            {
                var el = Convert.ToSingle(en.Current.y);
                min = Math.Min(el, min);
                max = Math.Max(el, max);
            }

            return (min, max);
        }
    }
}
