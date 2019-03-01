using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.ViewerContext;
using SkiaSharp;

namespace SINTEF.AutoActive.UI.Figures
{
    public class ByteLinePlot : LinePlot
    {
        internal ByteLinePlot(ITimeSeriesViewer viewer, TimeSynchronizedContext context) : base(viewer, context) { }

        protected override void CreatePath(SKPath plot, long offsetX, float scaleX, float offsetY, float scaleY)
        {
            CreatePath(plot, Viewer.GetCurrentBytes(), offsetX, scaleX, offsetY, scaleY);
        }
    }
}
