using System;
using System.Collections.Generic;
using System.Text;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Common;
using SkiaSharp;

namespace SINTEF.AutoActive.UI.Figures
{
    public class DoubleLinePlot : LinePlot
    {
        internal DoubleLinePlot(ITimeSeriesViewer viewer, DataViewerContext context) : base(viewer, context) { }

        protected override void CreatePath(SKPath plot, float offsetX, float scaleX, float offsetY, float scaleY)
        {
            var en = Viewer.GetCurrentDoubles().GetEnumerator();
            if (en.MoveNext())
            {
                plot.MoveTo(ScaleValue((float)en.Current.x, offsetX, scaleX), ScaleValue((float)en.Current.y, offsetY, scaleY));
                while (en.MoveNext()) plot.LineTo(ScaleValue((float)en.Current.x, offsetX, scaleX), ScaleValue((float)en.Current.y, offsetY, scaleY));
            }
        }
    }
}
