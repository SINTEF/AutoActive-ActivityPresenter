using System;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Pages.Player
{
    public delegate void PlayerSplitterDragStartHandler(PlayerSplitterView sender);
    public delegate void PlayerSplitterDraggedHandler(PlayerSplitterView sender, double x, double y);

    public class PlayerSplitterView : View
    {
        public static readonly GridLength DefaultWidth = 10;

        public event PlayerSplitterDragStartHandler DragStart;
        public event PlayerSplitterDraggedHandler Dragged;

        public StackOrientation Orientation
        {
            get => _orientation;
            set
            {
                _orientation = value;
                OrientationChanged?.Invoke(this, _orientation);
            }
        }

        public event EventHandler<StackOrientation> OrientationChanged;
        private StackOrientation _orientation;

        public void InvokeDragStart()
        {
            DragStart?.Invoke(this);
        }

        public void InvokeDragged(double x, double y)
        {
            Dragged?.Invoke(this, x, y);
        }
    }
}
