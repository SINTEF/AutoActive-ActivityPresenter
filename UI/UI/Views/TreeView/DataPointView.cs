using SINTEF.AutoActive.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    class DataPointView : MovableObject
    {
        public DataPointView():base()
        {
            this.ButtonColor = Color.DarkMagenta;
        }

        public DataPointView(DataTreeView parentTree, VisualizedStructure element) : base()
        {
            Element = element;
            ParentTree = parentTree;
        }

        public override void ObjectDroppedOn(IDraggable item)
        {
            if (item is DataPointView dataPointItem)
            {
                if (dataPointItem == this)
                {
                    return;
                }
            }

            throw new Exception("You can not add anything to a datapoint");
        }

        public override MovableObject CreateChildElement(VisualizedStructure element)
        {
            throw new NotImplementedException();
        }

    }
}
