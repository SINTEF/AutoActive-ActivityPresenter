using System;

namespace SINTEF.AutoActive.Plugins
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PluginAttribute : Attribute
    {
        public PluginAttribute(Type targetType, string kind)
        {
            Target = targetType;
            Kind = kind;
        }

        internal Type Target { get; }
        internal string Kind { get; }
    }
}
