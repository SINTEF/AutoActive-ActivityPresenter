using SINTEF.AutoActive.Databus;
using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Figures
{
    public class ImageView : SKCanvasView
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
            get { return (IDataPoint)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        public DataViewerContext ViewerContext
        {
            get { return (DataViewerContext)GetValue(ViewerContextProperty); }
            set { SetValue(ViewerContextProperty, value); }
        }

        private IImageViewer viewer;

        protected async override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == "Data" || propertyName == "ViewerContext")
            {
                if (Data != null && ViewerContext != null)
                {
                    viewer = await Data.CreateViewerIn(ViewerContext) as IImageViewer;
                    viewer.Changed += () =>
                    {
                        InvalidateSurface();
                    };
                }
            }
            if (propertyName == "Width" || propertyName == "Height")
            {
                if (Width > 0 && Height > 0)
                {
                    await viewer.SetSize((uint)Width, (uint)Height);
                    InvalidateSurface();
                }
            }
        }

        private SKBitmap bitmap;

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            if (viewer != null)
            {
                var frame = viewer.GetCurrentImage();
                if (frame.Frame != null && frame.Frame.Array != null)
                {
                    if (bitmap == null || bitmap.Width != frame.Width || bitmap.Height != frame.Height)
                    {
                        // Create a bitmap with the size of the canvas
                        bitmap = new SKBitmap((int)frame.Width, (int)frame.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
                    }

                    // Cannot copy more pixels than in the image or the size of the canvas
                    var toCopy = Math.Min(frame.Frame.Count, bitmap.Width * bitmap.Height * 4);
                    Marshal.Copy(frame.Frame.Array, frame.Frame.Offset, bitmap.GetPixels(), toCopy);

                    // Calculate the offset to put the image in the center of the canvas
                    var offsetX = (Width - frame.Width) / 2;
                    var offsetY = (Height - frame.Height) / 2;
                    
                    // Draw the image onto the canvas
                    canvas.DrawBitmap(bitmap, (float)offsetX, (float)offsetY);
                }
                
            }
        }
    }
}
