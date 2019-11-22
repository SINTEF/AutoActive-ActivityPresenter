using System;

namespace SINTEF.AutoActive.Plugins
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PluginAttribute : Attribute
    {
        public PluginAttribute(Type targetType, string kind, int priority)
        {
            Target = targetType;
            Kind = kind;
            Priority = priority;
        }

        internal Type Target { get; }
        internal string Kind { get; }
        public int Priority { get; }
    }
}
