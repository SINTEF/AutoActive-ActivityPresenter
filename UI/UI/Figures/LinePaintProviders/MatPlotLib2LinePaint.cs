using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace SINTEF.AutoActive.UI.Figures.LinePaintProviders
{
    public class MatPlotLib2LinePaint : ILinePaintProvider
    {
        private readonly SKPaint _lineTemplate = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeJoin = SKStrokeJoin.Miter,
            IsAntialias = true,
        };

        private readonly List<SKPaint> _paints = new List<SKPaint>();
        private int _index;

        public float StrokeWidth
        {
            get => _paints.First().StrokeWidth;
            set
            {
                foreach (var paint in _paints)
                {
                    paint.StrokeWidth = value;
                }
            }
        }

        public MatPlotLib2LinePaint(float width = 2)
        {
            // Colors from v2.0: https://matplotlib.org/_images/dflt_style_changes-1.png
            _lineTemplate.StrokeWidth = width;
            var colors = new[]
            {
                new SKColor(0xff1f77b4),
                new SKColor(0xffff7f0e),
                new SKColor(0xff2ca02c),
                new SKColor(0xffd62728),
                new SKColor(0xff9467bd),
                new SKColor(0xff8c564b),
                new SKColor(0xffe377c2),
                new SKColor(0xff7f7f7f),
                new SKColor(0xffbcbd22),
                new SKColor(0xff17becf)
            };
            foreach (var color in colors)
            {
                var paint = _lineTemplate.Clone();
                paint.Color = color;
                _paints.Add(paint);
            }
        }

        public SKPaint GetNextPaint()
        {
            var currentIndex = _index;
            if (++_index >= _paints.Count)
                _index = 0;

            return _paints[currentIndex];
        }
    }
}
