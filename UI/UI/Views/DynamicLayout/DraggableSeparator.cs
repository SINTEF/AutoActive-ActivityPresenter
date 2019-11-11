using System;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views.DynamicLayout
{
    public delegate void DraggableSeparatorDragStartHandler(DraggableSeparator sender, double x, double y);
    public delegate void DraggableSeparatorDraggedHandler(DraggableSeparator sender, double x, double y, double dx, double dy);

    public class DraggableSeparator : View
    {
        public event DraggableSeparatorDragStartHandler DragStart;
        public event DraggableSeparatorDraggedHandler DragStop;
        public event DraggableSeparatorDraggedHandler Dragged;
        public event EventHandler ViewRemoved;

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

        public void InvokeDragStart(double x, double y)
        {
            DragStart?.Invoke(this, x, y);
        }

        public void InvokeDragged(double x, double y, double dx, double dy)
        {
            Dragged?.Invoke(this, x, y, dx, dy);
        }

        public void InvokeDragStop(double x, double y, double dx, double dy)
        {
            DragStop?.Invoke(this, x, y, dx, dy);
        }

        public void InvokeRemoved()
        {
            ViewRemoved?.Invoke(this, new EventArgs());
        }
    }
}
