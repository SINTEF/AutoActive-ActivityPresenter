using System;
using System.Diagnostics;

namespace SINTEF.AutoActive.Plugins
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PluginAttribute : Attribute
    {
        public PluginAttribute(Type targetType, string kind)
        {
            Target = targetType;
            Kind = kind;
            /*
            if (!Target.IsAssignableFrom(Implementor))
            {
                Debug.WriteLine($"Implementor {implementorType.Name} doesn't implement {targetType.Name}. The plugin will not be available!")
                Debug.WriteLine("ERROR! Supplied plugin doesn't satisfy interface");
            }
            */
        }

        internal Type Target { get; private set; }
        internal string Kind { get; private set; }
    }
}
