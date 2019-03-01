using SINTEF.AutoActive.Databus.Common;
using SINTEF.AutoActive.Databus.Interfaces;
using SINTEF.AutoActive.Databus.ViewerContext;
using SINTEF.AutoActive.UI.Views;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SINTEF.AutoActive.UI.Figures
{
    public class ImageView : FigureView
    {
        public static async Task<ImageView> Create(IDataPoint datapoint, TimeSynchronizedContext context)
        {
            // TODO: Check that this datapoint has a type that can be used
            var viewer = await context.GetDataViewerFor(datapoint) as IImageViewer;
            return new ImageView(viewer, context);
        }

        protected IImageViewer Viewer { get; }

        protected ImageView(IImageViewer viewer, TimeSynchronizedContext context) : base(viewer, context)
        {
            Viewer = viewer;
        }

        protected override async void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (propertyName == "Width" || propertyName == "Height")
            {
                if (Width > 0 && Height > 0)
                {
                    await Viewer.SetSize((uint)Width, (uint)Height);
                    Canvas.InvalidateSurface();
                }
            }
        }

        private SKBitmap _bitmap;
        protected override void RedrawCanvas(SKCanvas canvas, SKImageInfo info)
        {
            var frame = Viewer.GetCurrentImage();
            if (frame.Frame == null || frame.Frame.Array == null) return;

            if (_bitmap == null || _bitmap.Width != frame.Width || _bitmap.Height != frame.Height)
            {
                // Create a bitmap with the size of the canvas
                _bitmap = new SKBitmap((int)frame.Width, (int)frame.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
            }

            // Cannot copy more pixels than in the image or the size of the canvas
            var toCopy = Math.Min(frame.Frame.Count, _bitmap.Width * _bitmap.Height * 4);
            Marshal.Copy(frame.Frame.Array, frame.Frame.Offset, _bitmap.GetPixels(), toCopy);

            // Calculate the offset to put the image in the center of the canvas
            var offsetX = (Width - frame.Width) / 2;
            var offsetY = (Height - frame.Height) / 2;

            // Draw the image onto the canvas
            canvas.DrawBitmap(_bitmap, (float)offsetX, (float)offsetY);
        }
    }
}
