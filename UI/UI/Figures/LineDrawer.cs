using System;
using System.Collections.Generic;
using System.Linq;
using SINTEF.AutoActive.Databus.Common;
using SkiaSharp;

namespace SINTEF.AutoActive.UI.Figures
{
    public interface ILineDrawer
    {
        void CreatePath(SKPath plot, SKRect drawRect, LineConfiguration lineConfig);

        float MinY { get; }
        float MaxY { get; }

        (float, float) GetVisibleYMinMax(int maxPoints);

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
            viewer.Changed += sender => _floatDataInvalidated = true;
            if (viewer.MinValueHint.HasValue) _minYValue = (float)viewer.MinValueHint.Value;
            if (viewer.MaxValueHint.HasValue) _maxYValue = (float)viewer.MaxValueHint.Value;
        }

        public void CreatePath(SKPath plot, SKRect drawRect, LineConfiguration lineConfig)
        {
            var offsetX = lineConfig.OffsetX;
            var scaleX = lineConfig.ScaleX;
            var offsetY = lineConfig.OffsetY;
            var scaleY = lineConfig.ScaleY;


            var floats = GetVisibleFloats(LinePlot.MaxPointsFromWidth(drawRect.Width));

            if (!floats.Any()) return;

            var width = drawRect.Width;

            var (firstX, firstY) = floats.First();
            var startX = Math.Max(LinePlot.ScaleX(firstX, offsetX, scaleX), 0);
            plot.MoveTo(startX + drawRect.Left, LinePlot.ScaleY(firstY, offsetY, scaleY));
            foreach (var (x, y) in floats.Skip(1))
            {
                var plotX = Math.Min(LinePlot.ScaleX(x, offsetX, scaleX), width);
                var valY = LinePlot.ScaleY(y, offsetY, scaleY);

                plot.LineTo(plotX + drawRect.Left, valY);

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (plotX == width)
                {
                    break;
                }
            }
        }

        // ---- Scaling ----
        private float _minYValue = -1;
        private float _maxYValue = 1;
        private bool _floatDataInvalidated = true;

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

        private List<(long, float)> _floatData;

        private IReadOnlyList<(long, float)> GetVisibleFloats(int maxPoints)
        {
            if (!_floatDataInvalidated)
            {
                return _floatData;
            }

            _floatData = new List<(long, float)>(maxPoints+1);
            var en = Viewer.GetCurrentData<T>().GetEnumerator(maxPoints);
            while (en.MoveNext())
            {
                var el = Convert.ToSingle(en.Current.y);
                _floatData.Add((en.Current.x, el));
            }

            _floatDataInvalidated = false;
            return _floatData;
        }

        public (float,float) GetVisibleYMinMax(int maxPoints)
        {
            var (min, max) = (float.MaxValue, float.MinValue);
            foreach (var (_, yData) in GetVisibleFloats(maxPoints))
            {
                min = Math.Min(yData, min);
                max = Math.Max(yData, max);
            }
            return (min, max);
        }
    }
}
