using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.UI.Interfaces
{
    public interface IDraggable
    {

    }
    public interface IDropCollector
    {
        void ObjectDroppedOn(IDraggable item);
    }
}
