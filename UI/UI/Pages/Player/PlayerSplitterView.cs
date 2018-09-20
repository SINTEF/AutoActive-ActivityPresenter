using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    public delegate void PlayerSplitterDragStartHandler();
    public delegate void PlayerSplitterDraggedHandler(double x, double y);

    public class PlayerSplitterView : View
    {
        public static readonly GridLength DefaultWidth = 10;

        public event PlayerSplitterDragStartHandler DragStart;
        public event PlayerSplitterDraggedHandler Dragged;

        public void InvokeDragStart()
        {
            DragStart?.Invoke();
        }

        public void InvokeDragged(double x, double y)
        {
            Dragged?.Invoke(x, y);
        }
    }
}
