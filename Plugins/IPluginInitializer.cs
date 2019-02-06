using System;
using System.Collections.Generic;

namespace SINTEF.AutoActive.Plugins
{
    public interface IPluginInitializer
    {
        IEnumerable<Type> Plugins { get; }
    }
}
