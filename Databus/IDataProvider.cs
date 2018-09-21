using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Databus
{
    public interface IDataProvider : IDataStructure
    {
        // An IDataProvider should also emit events from it's entire tree

        // TODO: It should not be added inside a datastructure as a child, that will cause strange behaviour of multiple events
        // Perhaps we should make sure of that somehow?
    }
}
