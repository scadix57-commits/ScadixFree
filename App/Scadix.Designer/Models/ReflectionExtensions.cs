using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Scadix.Designer
{
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Safely gets all types from an assembly, handling ReflectionTypeLoadException.
        /// </summary>
        public static IEnumerable<Type> GetSafeTypes(this Assembly assembly)
        {
            if (assembly == null) return Enumerable.Empty<Type>();

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                // Return only the types that were successfully loaded
                return e.Types.Where(t => t != null)!;
            }
            catch
            {
                // General fallback for other loading errors
                return Enumerable.Empty<Type>();
            }
        }
    }
}
