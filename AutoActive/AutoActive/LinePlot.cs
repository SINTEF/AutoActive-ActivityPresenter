using System;
using System.Collections.Generic;
using System.Text;

using System.Diagnostics;

using Xamarin.Forms;
using SkiaSharp;
using SkiaSharp.Views.Forms;

using Databus;
using System.Runtime.CompilerServices;

namespace AutoActive
{
    public class LinePlot : SKCanvasView
    {
        public static readonly BindableProperty DataProperty = BindableProperty.Create(
            propertyName: "Data",
            returnType: typeof(IDataPoint),
            declaringType: typeof(LinePlot),
            defaultValue: null
        );

        public static readonly BindableProperty ViewerContextProperty = BindableProperty.Create(
            propertyName: "ViewerContext",
            returnType: typeof(DataViewerContext),
            declaringType: typeof(LinePlot),
            defaultValue: null
        );

        public IDataPoint Data
        {
            get { return (IDataPoint)GetValue(DataProperty);  }
            set { SetValue(DataProperty, value); }
        }

        public DataViewerContext ViewerContext
        {
            get { return (DataViewerContext)GetValue(ViewerContextProperty); }
            set { SetValue(ViewerContextProperty, value); }
        }

        private IDataViewer viewer;

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == "Data" || propertyName == "ViewerContext")
            {
                if (Data != null && ViewerContext != null)
                {
                    viewer = Data.CreateViewerIn(ViewerContext);
                    viewer.Changed += () =>
                    {
                        InvalidateSurface();
                    };
                }
            }

        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.CornflowerBlue);

            if (viewer != null)
            {
                // Scale so that the whole canvas is x=[0,1] y=[-1,1]
                canvas.Scale(e.Info.Width, -e.Info.Height / 2);
                canvas.Translate(0, -1);

                // Draw the data
                SKPath plot = new SKPath();
                plot.MoveTo(0, 0);

                var data = viewer.GetCurrentFloat();
                var dx = 1f / data.Length;
                var x = 0f;
                foreach (float y in data)
                {
                    plot.LineTo(x, y);
                    x += dx;
                }

                canvas.DrawPath(plot, new SKPaint() { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 0.01f, StrokeJoin = SKStrokeJoin.Round, IsAntialias = true });
            }
        }
    }
}
