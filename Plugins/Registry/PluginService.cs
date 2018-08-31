using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using Xamarin.Forms.Internals;

namespace SINTEF.AutoActive.Plugins.Registry
{
    // FIXME: THREAD-SAFETY!
    public static class PluginService
    {
        static bool s_initialized = false;

        static readonly Dictionary<Type, Dictionary<string, PluginImplementorData>> PluginImplementations = new Dictionary<Type, Dictionary<string, PluginImplementorData>>();

        public static T Get<T>(string pluginName) where T : class
        {
            Initialize();

            var targetType = typeof(T);
            if (PluginImplementations.TryGetValue(targetType, out var targetPlugins))
            {
                if (targetPlugins.TryGetValue(pluginName, out var pluginImplementor))
                {
                    if (pluginImplementor.Instance == null)
                    {
                        pluginImplementor.Instance = Activator.CreateInstance(pluginImplementor.ImplementorType);
                    }
                    return (T)pluginImplementor.Instance;
                }
            }

            Debug.WriteLine($"ERROR! Plugin of type {targetType} with name {pluginName} not found");
            return null;
        }

        static void RegisterImplementor(Type targetType, Type implementorType, string pluginName, object implementor = null)
        {
            // Make sure the implementor type has an available constructor
            if (implementorType.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.WriteLine($"ERROR! Plugin {implementorType} doesn't expose a public parameterless constructor");
                return;
            }

            // Get the map for given target
            if (!PluginImplementations.TryGetValue(targetType, out var targetPlugins))
            {
                targetPlugins = new Dictionary<string, PluginImplementorData>();
                PluginImplementations[targetType] = targetPlugins;
            }
            // Set the implementor for the given name
            if (!targetPlugins.TryGetValue(pluginName, out var namedPlugin))
            {
                targetPlugins[pluginName] = new PluginImplementorData { ImplementorType = implementorType, Instance = implementor };
            }
            else
            {
                Debug.WriteLine($"ERROR! Plugin type {targetType} for name {pluginName} is already implemented by {namedPlugin.ImplementorType} (tried by {implementorType}).");
            }
        }

        public static void Register<T, Timpl>(string pluginName, Timpl implementor = null) where T : class where Timpl : class, T
        {
            RegisterImplementor(typeof(T), typeof(Timpl), pluginName, implementor);
        }

        static void Initialize()
        {
            if (s_initialized) return;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (Registrar.ExtraAssemblies != null)
            {
                assemblies = assemblies.Union(Registrar.ExtraAssemblies).ToArray();
            }

            Initialize(assemblies);
        }

        internal static void Initialize(Assembly[] assemblies)
        {
            if (s_initialized) return;

            Type targetAttrType = typeof(PluginAttribute);

            foreach (Assembly assembly in assemblies)
            {
                Attribute[] attributes;
                try
                {
                    attributes = assembly.GetCustomAttributes(targetAttrType).ToArray();
                }
                catch (System.IO.FileNotFoundException)
                {
                    continue;
                }

                if (attributes.Length == 0) continue;

                foreach (PluginAttribute attribute in attributes)
                {
                    RegisterImplementor(attribute.Target, attribute.Implementor, attribute.Name);
                }
            }

            s_initialized = true;
        }

        class PluginImplementorData
        {
            public object Instance { get; set; }
            public Type ImplementorType { get; set; }
        }
    }
}
