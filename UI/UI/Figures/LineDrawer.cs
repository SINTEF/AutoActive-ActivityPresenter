using System;
using SINTEF.AutoActive.Databus.Common;
using SkiaSharp;

namespace SINTEF.AutoActive.UI.Figures
{
    public interface ILineDrawer
    {
        void CreatePath(SKPath plot, SKImageInfo info, LineConfiguration lineConfig);

        float MinY { get; }
        float MaxY { get; }
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

        public void CreatePath(SKPath plot, SKImageInfo info, LineConfiguration lineConfig)
        {
            var offsetX = lineConfig.OffsetX;
            var scaleX = lineConfig.ScaleX;
            var offsetY = lineConfig.OffsetY;
            var scaleY = lineConfig.ScaleY;

            var en = Viewer.GetCurrentData<T>().GetEnumerator(MaxItems);
            if (!en.MoveNext()) return;

            var width = info.Width;

            var startX = Math.Max(LinePlot.ScaleX(en.Current.x, offsetX, scaleX), LinePlot.ScaleX(offsetX, offsetX, scaleX));
            plot.MoveTo(startX, LinePlot.ScaleY(Convert.ToSingle(en.Current.y), offsetY, scaleY));
            var done = false;
            while (en.MoveNext() && !done)
            {
                var plotX = LinePlot.ScaleX(en.Current.x, offsetX, scaleX);
                if (plotX > width)
                {
                    plotX = width;
                    done = true;
                }
                plot.LineTo(plotX, LinePlot.ScaleY(Convert.ToSingle(en.Current.y), offsetY, scaleY));
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
    }
}
