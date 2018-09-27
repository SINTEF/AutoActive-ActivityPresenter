using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Plugins
{
    public interface IPluginInitializer
    {
        IEnumerable<Type> Plugins { get; }
    }
}
