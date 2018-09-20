using System;
using System.Collections.Generic;
using System.Text;

namespace SINTEF.AutoActive.Plugins
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class PluginTypeAttribute : Attribute
    {
        public PluginTypeAttribute()
        {
            AllowMultipleImplementations = false;
            UseSingletonInstance = true;
        }

        public bool AllowMultipleImplementations { get; set; }
        public bool UseSingletonInstance { get; set; }
    }
}
