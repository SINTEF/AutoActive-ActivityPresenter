using SINTEF.AutoActive.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.UI.Views.TreeView
{
    class BranchView : MovableObject
    {
        public BranchView() : base()
        {

        }
        public override void ObjectDroppedOn(IDraggable item)
        {
            ParentTree?.ObjectDroppedOn(this, item);
        }

        public override MovableObject CreateChildElement(VisualizedStructure element)
        {
            return new BranchView { ParentTree = ParentTree, Element = element };
        }
    }
}
