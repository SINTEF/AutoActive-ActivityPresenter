using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if !DEBUG
using Xamarin.Forms;
#endif

namespace SINTEF.AutoActive.Plugins
{
    internal class DescendingComparer<TKey> : IComparer<int>
    {
        public int Compare(int x, int y)
        {
            var ret = y.CompareTo(x);
            // Allow duplicates by handle equality as being greater. This breaks Remove(key) and IndexOfKey(key)
            // Source: https://stackoverflow.com/questions/5716423/c-sharp-sortable-collection-which-allows-duplicate-keys
            return ret == 0 ? 1 : ret;
        }
    }

    // FIXME: THREAD-SAFETY!
    public static class PluginService
    {
        private static readonly Dictionary<Type, PluginTypeAttribute> PluginTargetTypes = new Dictionary<Type, PluginTypeAttribute>();
        private static readonly Dictionary<Type, Dictionary<string, SortedList<int, PluginImplementorData>>> PluginImplementors = new Dictionary<Type, Dictionary<string, SortedList<int, PluginImplementorData>>>();

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
            if (PluginTargetTypes.TryGetValue(pluginTarget, out var pluginTypeAttribute))
            {
                return pluginTypeAttribute;
            }
            else
            {
                var pluginTypeAttributes = pluginTarget.GetCustomAttributes(typeof(PluginTypeAttribute), false);
                if (pluginTypeAttributes.Length != 1)
                {
                    Debug.WriteLine($"The plugin-type {pluginTarget.Name} does not have a single PluginType attribute. No plugins of this type will be provided!", "Error");
                    PluginTargetTypes[pluginTarget] = null;
                }
                else
                {
                    PluginTargetTypes[pluginTarget] = pluginTypeAttributes[0] as PluginTypeAttribute;
                }
                return PluginTargetTypes[pluginTarget];
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

            // If the implementor is not provided, we need to be able to construct one ourselves
            if (plugin.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.WriteLine($"Provided type {plugin.Name} does has no parameterless constructor. The type will not be used as a plugin!", "Error");
                return;
            }
            // Check the plugin-type of the target
            var targetAttribute = GetOrRegisterPluginType(pluginAttribute.Target);
            if (targetAttribute == null) return;

            // Get the plugins for this target type
            if (!PluginImplementors.TryGetValue(pluginAttribute.Target, out var targetPlugins))
            {
                targetPlugins = new Dictionary<string, SortedList<int, PluginImplementorData>>();
                PluginImplementors[pluginAttribute.Target] = targetPlugins;
            }

            if (!targetPlugins.TryGetValue(pluginAttribute.Kind, out var targetKindPlugins))
            {
                // Create a new list containing this plugin
                targetKindPlugins = new SortedList<int, PluginImplementorData>(new DescendingComparer<int>())
                {
                    {
                        PluginAttribute.DefaultPriority,
                        new PluginImplementorData
                        {
                            ImplementorType = plugin,
                            Instance = null,
                            PluginType = targetAttribute,
                        }
                    }
                };
                targetPlugins[pluginAttribute.Kind] = targetKindPlugins;
                return;
            }

            // Check if multiple implementors of this plugin-type for every kind is allowed
            if (!targetAttribute.AllowMultipleImplementations)
            {
                // If not, show an error
                Debug.WriteLine($"Plugin-type {pluginAttribute.Target.Name} with kind '{pluginAttribute.Kind}' is already provided by {targetKindPlugins.First().Value.ImplementorType.Name}. The type {plugin.Name} will not be used as a plugin!",
                    "Error");
                return;
            }

            // If yes, just add another
            targetKindPlugins.Add(pluginAttribute.Priority, new PluginImplementorData
            {
                ImplementorType = plugin,
                Instance = null,
                PluginType = targetAttribute,
                Priority = pluginAttribute.Priority
            });
        }

        static T[] GetImplementors<T>(string kind, bool isSingle)
        {
            var targetType = typeof(T);
            var pluginTypeAttribute = GetOrRegisterPluginType(targetType);
            if (pluginTypeAttribute == null) return default;

            // Ensure consistent data
            if (pluginTypeAttribute.AllowMultipleImplementations && isSingle)
            {
                throw new InvalidOperationException($"Plugin-type {targetType.Name} is not a single-implementor type.");
            }
            if (!pluginTypeAttribute.AllowMultipleImplementations && !isSingle)
            {
                throw new InvalidOperationException($"Plugin-type {targetType.Name} is not a multi-implementor type.");
            }


            // Search for matching implementor(s)
            if (!PluginImplementors.TryGetValue(targetType, out var targetImplementors))
            {
                return default;
            }
            if (!targetImplementors.TryGetValue(kind, out var targetKindImplementors))
            {
                return default;
            }

            return EnsureInstanceExists<T>(targetKindImplementors, pluginTypeAttribute);
        }

        private static T[] EnsureInstanceExists<T>(SortedList<int, PluginImplementorData> targetKindImplementors, PluginTypeAttribute pluginTypeAttribute)
        {
            var result = new List<T>(targetKindImplementors.Count);
            foreach (var implementorPair in targetKindImplementors)
            {
                var implementor = implementorPair.Value;
                if (pluginTypeAttribute.UseSingletonInstance)
                {
                    if (implementor.Instance == null)
                    {
                        implementor.Instance = Activator.CreateInstance(implementor.ImplementorType);
                    }

                    result.Add((T) implementor.Instance);

                    continue;
                }

                result.Add((T) Activator.CreateInstance(implementor.ImplementorType));
            }

            return result.ToArray();
        }

        public static T GetSingle<T>(string kind) where T : class
        {
            var implementors = GetImplementors<T>(kind, true);
            if (implementors == null) return null;
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
            if (PluginImplementors.TryGetValue(targetType, out var targetImplementors))
            {
                return targetImplementors.Keys.ToArray();
            }

            return Array.Empty<string>();
        }

        public static Dictionary<string, List<Type>> GetExtensionTypes<T>()
        {
            var targetType = typeof(T);
            if (!PluginImplementors.TryGetValue(targetType, out var targetImplementors))
            {
                return new Dictionary<string, List<Type>>();
            }

            var ret = new Dictionary<string, List<Type>>();
            foreach (var item in targetImplementors)
            {
                ret[item.Key] = item.Value.Select(el => el.Value.ImplementorType).ToList();
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

        private class PluginImplementorData
        {
            public object Instance { get; set; }
            public Type ImplementorType { get; set; }
            public PluginTypeAttribute PluginType { get; set; }
            public int Priority { get; internal set; }
        }
    }
}
