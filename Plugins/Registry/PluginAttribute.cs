using System;
using System.Diagnostics;

namespace SINTEF.AutoActive.Plugins.Registry
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class PluginAttribute : Attribute
    {
        public PluginAttribute(Type targetType, Type implementorType, string name)
        {
            Target = targetType;
            Implementor = implementorType;
            Name = name;

            if (!Target.IsAssignableFrom(Implementor))
            {
                Debug.WriteLine("ERROR! Supplied plugin doesn't satisfy interface");
            }
        }

        internal Type Target { get; private set; }
        internal Type Implementor { get; private set; }
        internal string Name { get; private set; }
    }
}
