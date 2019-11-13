using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using SINTEF.AutoActive.UI.Droid.Views;
using SINTEF.AutoActive.UI.Pages.Player;
using SINTEF.AutoActive.UI.Views.DynamicLayout;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

using View = Android.Views.View;
using Color = Android.Graphics.Color;

[assembly: ExportRenderer(typeof(DraggableSeparator), typeof(DraggableSeparatorViewRenderer))]
namespace SINTEF.AutoActive.UI.Droid.Views
{
    public class DraggableSeparatorViewRenderer : ViewRenderer<DraggableSeparator, View>
    {
        readonly Context context;
        View element;

        public DraggableSeparatorViewRenderer(Context context) : base(context)
        {
            this.context = context;
        }

        protected override void OnElementChanged(ElementChangedEventArgs<DraggableSeparator> e)
        {
            base.OnElementChanged(e);
            if (Control == null)
            {
                element = new View(context);
                element.SetBackgroundColor(Color.White);
                element.Touch += Touch;
                SetNativeControl(element);
            }
        }

        // Track pointer movement throughout the window
        int? capturedPointerId;
        float dragStartRawX;
        float dragStartRawY;

        private new void Touch(object sender, TouchEventArgs e)
        {
            // TODO: Which pointer index does RawX and RawY work with?
            if (capturedPointerId == null && e.Event.Action == MotionEventActions.Down)
            {
                // Use the first ID
                capturedPointerId = e.Event.GetPointerId(0);
                dragStartRawX = e.Event.RawX;
                dragStartRawY = e.Event.RawY;
                Element?.InvokeDragStart(dragStartRawX, dragStartRawY);
            }
            else if (capturedPointerId != null && (e.Event.Action == MotionEventActions.Move || e.Event.Action == MotionEventActions.Up))
            {
                var index = e.Event.FindPointerIndex(capturedPointerId.Value);
                if (index >= 0)
                {
                    if (e.Event.Action == MotionEventActions.Move)
                    {
                        var movedRawX = e.Event.RawX - dragStartRawX;
                        var movedRawY = e.Event.RawY - dragStartRawY;
                        var scale = Resources.DisplayMetrics.Density;

                        Element?.InvokeDragged(dragStartRawX, dragStartRawY, movedRawX / scale, movedRawY / scale);
                    }
                    else
                    {
                        capturedPointerId = null;
                    }
                }
            }
        }
    }
}