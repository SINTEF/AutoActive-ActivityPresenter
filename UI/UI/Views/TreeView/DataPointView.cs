using SINTEF.AutoActive.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    class DataPointView : MovableObject
    {
        public DataPointView():base()
        {

        }

        public DataPointView(DataTreeView parentTree, VisualizedStructure element) : base()
        {
            Element = element;
            ParentTree = parentTree;
        }

        public override void ObjectDroppedOn(IDraggable item)
        {
            throw new Exception("You can not add a folder to data");
        }

        public override MovableObject CreateChildElement(VisualizedStructure element)
        {
            throw new NotImplementedException();
        }

    }
}
