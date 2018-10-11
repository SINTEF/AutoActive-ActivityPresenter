using System;
using System.Collections.Generic;
using System.Text;
using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.ViewerContext;
using SkiaSharp;

namespace SINTEF.AutoActive.UI.Figures
{
    public class IntLinePlot : LinePlot
    {
        internal IntLinePlot(ITimeSeriesViewer viewer, TimeSynchronizedContext context) : base(viewer, context) { }

        protected override void CreatePath(SKPath plot, long offsetX, float scaleX, float offsetY, float scaleY)
        {
            var en = Viewer.GetCurrentInts().GetEnumerator();
            if (en.MoveNext())
            {
                plot.MoveTo(ScaleX(en.Current.x, offsetX, scaleX), ScaleY(en.Current.y, offsetY, scaleY));
                while (en.MoveNext()) plot.LineTo(ScaleX(en.Current.x, offsetX, scaleX), ScaleY(en.Current.y, offsetY, scaleY));
            }
        }
    }
}
