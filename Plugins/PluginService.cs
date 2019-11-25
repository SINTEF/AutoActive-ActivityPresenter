using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if !DEBUG
using Xamarin.Forms;
#endif

namespace SINTEF.AutoActive.Plugins
{
    // FIXME: THREAD-SAFETY!
    public static class PluginService
    {
        static readonly Dictionary<Type, PluginTypeAttribute> pluginTargetTypes = new Dictionary<Type, PluginTypeAttribute>();
        static readonly Dictionary<Type, Dictionary<string, SortedSet<PluginImplementorData>>> pluginImplementors = new Dictionary<Type, Dictionary<string, SortedSet<PluginImplementorData>>>();

        static PluginService()
        {
#if DEBUG
            var initializers = DependencyHandler.GetAllInstances<IPluginInitializer>();
            foreach (var initializer in initializers)
            {
#else
            var initializer = DependencyService.Get<IPluginInitializer>();
#endif
                // Map out all provided plugins
                foreach (var plugin in initializer.Plugins)
                {
                    // Check if the type has any PluginAttributes
                    var pluginAttributes = plugin.GetCustomAttributes(typeof(PluginAttribute), true);
                    if (pluginAttributes.Length < 1)
                    {
                        Debug.WriteLine(
                            $"Provided type {plugin.Name} has no Plugin attributes. This type will not be used!",
                            "Error");
                    }

                    foreach (var pluginAttribute in pluginAttributes)
                    {
                        RegisterPlugin(plugin, pluginAttribute as PluginAttribute);
                    }
                }
#if DEBUG
            }
#endif
        }

        private static PluginTypeAttribute GetOrRegisterPluginType(Type pluginTarget)
        {
            if (pluginTargetTypes.TryGetValue(pluginTarget, out var pluginTypeAttribute))
            {
                return pluginTypeAttribute;
            }
            else
            {
                var pluginTypeAttributes = pluginTarget.GetCustomAttributes(typeof(PluginTypeAttribute), false);
                if (pluginTypeAttributes.Length != 1)
                {
                    Debug.WriteLine($"The plugin-type {pluginTarget.Name} does not have a single PluginType attribute. No plugins of this type will be provided!", "Error");
                    pluginTargetTypes[pluginTarget] = null;
                }
                else
                {
                    pluginTargetTypes[pluginTarget] = pluginTypeAttributes[0] as PluginTypeAttribute;
                }
                return pluginTargetTypes[pluginTarget];
            }
        }

        private static void RegisterPlugin(Type plugin, PluginAttribute pluginAttribute)
        {
            // Check that it implements the proper interface
            if (!pluginAttribute.Target.IsAssignableFrom(plugin))
            {
                Debug.WriteLine($"Provided type {plugin.Name} does not implement {pluginAttribute.Target.Name}. The type will not be used as a plugin!", "Error");
                return;
            }
            // Register this type
            Register(plugin, pluginAttribute);
        }

        private static void Register(Type plugin, PluginAttribute pluginAttribute)
        {

            object implementor = null;

            // If the implementor is not provided, we need to be able to construct one ourself
            if (plugin.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.WriteLine($"Provided type {plugin.Name} does has no parameterless constructor. The type will not be used as a plugin!", "Error");
                return;
            }
            // Check the plugin-type of the target
            var targetAttribute = GetOrRegisterPluginType(pluginAttribute.Target);
            if (targetAttribute != null)
            {
                // Get the plugins for this target type
                if (!pluginImplementors.TryGetValue(pluginAttribute.Target, out var targetPlugins))
                {
                    targetPlugins = new Dictionary<string, SortedSet<PluginImplementorData>>();
                    pluginImplementors[pluginAttribute.Target] = targetPlugins;
                }

                if (targetPlugins.TryGetValue(pluginAttribute.Kind, out var targetKindPlugins))
                {
                    // Check if multiple implementors of this plugin-type for every kind is allowed
                    if (targetAttribute.AllowMultipleImplementations)
                    {
                        // If yes, just add another
                        targetKindPlugins.Add(new PluginImplementorData
                        {
                            ImplementorType = plugin,
                            Instance = implementor,
                            PluginType = targetAttribute,
                            Priority = pluginAttribute.Priority
                        });
                    }
                    else
                    {
                        // If not, show an error
                        Debug.WriteLine($"Plugin-type {pluginAttribute.Target.Name} with kind '{pluginAttribute.Kind}' is already provided by {targetKindPlugins.First().ImplementorType.Name}. The type {plugin.Name} will not be used as a plugin!", "Error");
                    }
                }
                else
                {
                    // Create a new list containing this plugin
                    targetKindPlugins = new SortedSet<PluginImplementorData>();
                    targetKindPlugins.Add(new PluginImplementorData
                    {
                        ImplementorType = plugin,
                        Instance = implementor,
                        PluginType = targetAttribute,
                    });
                    targetPlugins[pluginAttribute.Kind] = targetKindPlugins;
                }
            }
        }

        static T[] GetImplementors<T>(string kind, bool isSingle)
        {
            var targetType = typeof(T);
            var pluginTypeAttribute = GetOrRegisterPluginType(targetType);
            if (pluginTypeAttribute != null)
            {
                if (pluginTypeAttribute.AllowMultipleImplementations && isSingle)
                {
                    throw new InvalidOperationException($"Plugin-type {targetType.Name} is not a single-implementor type.");
                }
                else if (!pluginTypeAttribute.AllowMultipleImplementations && !isSingle)
                {
                    throw new InvalidOperationException($"Plugin-type {targetType.Name} is not a multi-implementor type.");
                }
                // Find a matching implementor
                if (pluginImplementors.TryGetValue(targetType, out var targetImplementors))
                {
                    if (targetImplementors.TryGetValue(kind, out var targetKindImplementors))
                    {
                        var result = new List<T>(targetKindImplementors.Count);

                        foreach (var implementor in targetKindImplementors)
                        {
                            if (pluginTypeAttribute.UseSingletonInstance)
                            {
                                if (implementor.Instance == null)
                                {
                                    implementor.Instance = Activator.CreateInstance(implementor.ImplementorType);
                                }
                                result.Add((T)implementor.Instance);
                            }
                            else
                            {
                                result.Add((T)Activator.CreateInstance(implementor.ImplementorType));
                            }
                        }
                        return result.ToArray();
                    }
                }
            }
            return Array.Empty<T>();
        }

        public static T GetSingle<T>(string kind) where T : class
        {
            var implementors = GetImplementors<T>(kind, true);
            return implementors.Length == 0 ? null : implementors[0];
        }

        public static T[] GetAll<T>(string kind) where T : class
        {
            var targetType = typeof(T);
            var implementors = GetImplementors<T>(kind, false);
            return implementors;
        }

        public static string[] GetKinds<T>() where T : class
        {
            var targetType = typeof(T);
            if (pluginImplementors.TryGetValue(targetType, out var targetImplementors))
            {
                return targetImplementors.Keys.ToArray();
            }

            return Array.Empty<string>();
        }

        public static Dictionary<string, List<Type>> GetExtensionTypes<T>()
        {
            var targetType = typeof(T);
            if (!pluginImplementors.TryGetValue(targetType, out var targetImplementors))
            {
                return new Dictionary<string, List<Type>>();
            }

            var ret = new Dictionary<string, List<Type>>();
            foreach (var item in targetImplementors)
            {
                ret[item.Key] = item.Value.Select(el => el.ImplementorType).ToList();
            }
            return ret;
        }

        public static Dictionary<Type, List<string>> GetTypeExtensions<T>()
        {
            var orig = GetExtensionTypes<T>();
            if (orig.Count == 0)
            {
                return new Dictionary<Type, List<string>>();
            }

            var ret = new Dictionary<Type, List<string>>();
            foreach (var el in orig)
            {
                var ext = el.Key;
                foreach (var type in el.Value)
                {
                    if (!ret.TryGetValue(type, out var list))
                    {
                        list = new List<string>();
                        ret[type] = list;
                    }
                    list.Add(ext);
                }
            }

            return ret;
        }

        class PluginImplementorData : IComparable<PluginImplementorData>
        {
            public object Instance { get; set; }
            public Type ImplementorType { get; set; }
            public PluginTypeAttribute PluginType { get; set; }
            public int Priority { get; internal set; }

            public int CompareTo(PluginImplementorData other)
            {
                // Reversed sorting to get highest priority first
                return other.Priority.CompareTo(Priority);
            }
        }
    }
}
