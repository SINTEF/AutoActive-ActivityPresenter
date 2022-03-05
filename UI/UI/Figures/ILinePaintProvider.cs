using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace SINTEF.AutoActive.UI.Figures
{
    public interface ILinePaintProvider
    {
        SKPaint GetNextPaint();
        SKPaint GetIndexedPaint(int index);
    }
}
