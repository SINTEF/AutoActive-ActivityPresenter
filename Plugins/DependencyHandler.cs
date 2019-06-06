using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;


//These methods seems to only work in debug. Woraround: use DependencyService.Get<T>
#if DEBUG
namespace SINTEF.AutoActive.Plugins
{
    public static class DependencyHandler
    {
        private static Assembly[] GetAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies();
        }
        public static IEnumerable<T> GetAllInstances<T>()
        {
            var assemblies = GetAssemblies();

            // Get all plugin initializers
            var type = typeof(T);
            var parentTypes = assemblies
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && p.IsClass && !p.IsAbstract)
                .Select(p => (T)Activator.CreateInstance(p));
            return parentTypes;
        }

        public static T GetInstance<T>()
        {
            var assemblies = GetAssemblies();

            // Get all plugin initializers
            var type = typeof(T);
            var parentType = assemblies
                .SelectMany(s => s.GetTypes())
                .SingleOrDefault(p => type.IsAssignableFrom(p) && p.IsClass && !p.IsAbstract);

            if (parentType == null)
                return default;

            return (T)Activator.CreateInstance(parentType);
        }
    }
}
#endif