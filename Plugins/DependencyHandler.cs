using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace SINTEF.AutoActive.Plugins
{
    public static class DependencyHandler
    {
        public static IEnumerable<T> GetAllInstances<T>()
        {
            // Get all plugin initializers
            var type = typeof(T);
            var parentTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && p.IsClass && !p.IsAbstract)
                .Select(p => (T)Activator.CreateInstance(p));
            return parentTypes;
        }

        public static T GetInstance<T>()
        {
            // Get all plugin initializers
            var type = typeof(T);
            var parentType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .SingleOrDefault(p => type.IsAssignableFrom(p) && p.IsClass && !p.IsAbstract);

            if (parentType == null)
                return default;

            return (T)Activator.CreateInstance(parentType);
        }
    }
}
