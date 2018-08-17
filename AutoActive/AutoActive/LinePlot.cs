using System;
using System.Collections.Generic;
using System.Text;

using SkiaSharp;
using SkiaSharp.Views.Forms;

namespace AutoActive
{
    public class LinePlot : SKCanvasView
    {
        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);

            var canvas = e.Surface.Canvas;

            canvas.Clear(SKColors.CornflowerBlue);
        }
    }
}
