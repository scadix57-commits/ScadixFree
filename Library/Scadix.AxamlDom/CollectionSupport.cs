

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Metadata;
using Avalonia.Styling;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;


namespace Scadix.AxamlDom
{
    public interface IAddChild : Avalonia.Metadata.IAddChild
    {
        void AddText(string v);
    }
    /// <summary>
    /// Static class containing helper methods to work with collections (like the XamlParser does)
    /// </summary>
    public static class CollectionSupport
    {
        /// <summary>
        /// Gets if the type is considered a collection in XAML.
        /// </summary>
        public static bool IsCollectionType(Type type)
        {
            return type != typeof(LineBreak) && (
               typeof(IList).IsAssignableFrom(type)
               || type.GetInterfaces().Any(x => x.IsGenericType && (
                   x.GetGenericTypeDefinition() == typeof(IList<>) ||
                   x.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                   x.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>) ||
                   x.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) ||
                   x.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
               || typeof(IAddChild).IsAssignableFrom(type)
               || typeof(IDictionary).IsAssignableFrom(type));
        }

        /// <summary>
        /// Gets if the collection type <paramref name="col"/> can accepts items of type
        /// <paramref name="item"/>.
        /// </summary>
        public static bool CanCollectionAdd(Type col, Type item)
        {
            var e = col.GetInterface("IEnumerable`1");
            if (e != null && e.IsGenericType)
            {
                var a = e.GetGenericArguments()[0];
                return a.IsAssignableFrom(item);
            }
            return true;
        }

        /// <summary>
        /// Gets if the collection type <paramref name="col"/> can accept the specified items.
        /// </summary>
        public static bool CanCollectionAdd(Type col, IEnumerable items)
        {
            foreach (var item in items)
            {
                if (!CanCollectionAdd(col, item.GetType())) return false;
            }
            return true;
        }

        /// <summary>
        /// Adds a value to the end of a collection.
        /// </summary>
        public static void AddToCollection(Type collectionType, object collectionInstance, XamlPropertyValue newElement)
        {
            IAddChild addChild = collectionInstance as IAddChild;
            if (addChild != null)
            {
                if (newElement is XamlTextValue)
                {
                    addChild.AddText((string)newElement.GetValueFor(null));
                }
                else
                {
                    addChild.AddChild(newElement.GetValueFor(null));
                }
                return;
            }

            if (collectionInstance is IDictionary || (collectionInstance != null && collectionInstance.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))))
            {
                object val = newElement.GetValueFor(null);
                object key = newElement is XamlObject ? ((XamlObject)newElement).GetXamlAttribute("Key") : null;
                if (key == null || (key as string) == "")
                {
                    if (val is ControlTheme)
                        key = ((ControlTheme)val).TargetType;
                }
                if (key == null || (key as string) == "")
                    key = val;

                if (collectionInstance is IDictionary legacyDict)
                {
                    legacyDict.Add(key, val);
                }
                else
                {
                    // Handle generic dictionary via reflection if legacy IDictionary is missing
                    var addMethod = collectionInstance.GetType().GetMethod("Add", new[] { key?.GetType() ?? typeof(object), val?.GetType() ?? typeof(object) });
                    if (addMethod == null)
                    {
                        // Fallback to searching for any Add method with 2 parameters
                        addMethod = collectionInstance.GetType().GetMethods().FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 2);
                    }
                    addMethod?.Invoke(collectionInstance, new[] { key, val });
                }
            }
            else
            {


                var elementValue = newElement.GetValueFor(null);

                // Special handling for ControlTheme.Children
                if (IsControlThemeChildren(collectionType, collectionInstance) && elementValue is Setter setter)
                {
                    elementValue = WrapSetterInStyle(collectionInstance, setter);
                }

                var addMethod = GetCollectionMethod(collectionType, "Add", 1)
                              ?? (collectionInstance != null ? GetCollectionMethod(collectionInstance.GetType(), "Add", 1) : null);

                if (addMethod != null) addMethod.Invoke(collectionInstance, new[] { elementValue });
                else
                {
                    collectionType.InvokeMember(
                     "Add", BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance,
                     null, collectionInstance,
                     new object[] { newElement.GetValueFor(null) },
                     CultureInfo.InvariantCulture);
                }

            }
        }

        /// <summary>
        /// Adds a value at the specified index in the collection.
        /// </summary>
        public static bool Insert(Type collectionType, object collectionInstance, XamlPropertyValue newElement, int index)
        {
            object value = newElement.GetValueFor(null);

            // Using IList, with possible Add instead of Insert, was primarily added as a workaround
            // for a peculiarity (or bug) with collections inside System.Windows.Input namespace.
            // See CollectionTests.InputCollectionsPeculiarityOrBug test method for details.
            var list = collectionInstance as IList;
            if (list != null)
            {
                if (list.Count == index)
                {
                    list.Add(value);
                }
                else
                {
                    list.Insert(index, value);
                }
                return true;
            }
            else
            {
                // Try generic IList via reflection
                var genericListInterface = collectionInstance?.GetType().GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
                if (genericListInterface != null)
                {
                    var insertMethod = collectionInstance.GetType().GetMethod("Insert");
                    if (insertMethod != null)
                    {
                        insertMethod.Invoke(collectionInstance, new object[] { index, value });
                        return true;
                    }
                }

                var hasInsert = collectionType.GetMethods().Any(x => x.Name == "Insert");

                if (hasInsert)
                {
                    collectionType.InvokeMember(
                        "Insert", BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance,
                        null, collectionInstance,
                        new object[] { index, value },
                        CultureInfo.InvariantCulture);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a value at the specified index in the collection. A return value indicates whether the Insert succeeded.
        /// </summary>
        /// <returns>True if the Insert succeeded, false if the collection type does not support Insert.</returns>
        internal static bool TryInsert(Type collectionType, object collectionInstance, XamlPropertyValue newElement, int index)
        {
            try
            {
                return Insert(collectionType, collectionInstance, newElement, index);
            }
            catch (MissingMethodException)
            {
                return false;
            }
        }

        static readonly Type[] RemoveAtParameters = { typeof(int) };

        /// <summary>
        /// Removes the item at the specified index of the collection.
        /// </summary>
        /// <returns>True if the removal succeeded, false if the collection type does not support RemoveAt.</returns>
        public static bool RemoveItemAt(Type collectionType, object collectionInstance, int index)
        {
            MethodInfo m = collectionType.GetMethod("RemoveAt", RemoveAtParameters);
            if (m != null)
            {
                m.Invoke(collectionInstance, new object[] { index });
                return true;
            }
            else
            {
                // Try IList interface
                if (collectionInstance is IList list)
                {
                    list.RemoveAt(index);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Removes an item instance from the specified collection.
        /// </summary>
        public static void RemoveItem(Type collectionType, object collectionInstance, object item)
        {
            if (collectionInstance is IList list)
            {
                list.Remove(item);
                return;
            }

            if (collectionInstance is IDictionary dict)
            {
                dict.Remove(item);
                return;
            }

            collectionType.InvokeMember(
                "Remove", BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance,
                null, collectionInstance,
                new object[] { item },
                CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Removes an item instance from the specified collection.
        /// </summary>
        internal static void RemoveItem(Type collectionType, object collectionInstance, object item, XamlPropertyValue element)
        {
            var dictionary = collectionInstance as IDictionary;
            var xamlObject = element as XamlObject;

            if (dictionary != null && xamlObject != null)
            {
                dictionary.Remove(xamlObject.GetXamlAttribute("Key"));
            }
            else if (collectionInstance != null && xamlObject != null && collectionInstance.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
            {
                var key = xamlObject.GetXamlAttribute("Key");
                var removeMethod = collectionInstance.GetType().GetMethod("Remove", new[] { key?.GetType() ?? typeof(object) });
                removeMethod?.Invoke(collectionInstance, new[] { key });
            }
            else
            {
                RemoveItem(collectionType, collectionInstance, item);
            }
        }

        private static bool IsControlThemeChildren(Type collectionType, object collectionInstance)
        {
            if (collectionInstance == null || collectionType == null || !collectionType.IsGenericType) return false;
            var genericArgs = collectionType.GetGenericArguments();
            return genericArgs.Length > 0 && genericArgs[0] == typeof(IStyle) &&
                   (collectionType.GetGenericTypeDefinition() == typeof(IList<>) ||
                    collectionType.GetGenericTypeDefinition() == typeof(List<>) ||
                    collectionType.GetGenericTypeDefinition() == typeof(ICollection<>));
        }

        private static Style WrapSetterInStyle(object collectionInstance, Setter setter)
        {
            Type targetType = null;
            try
            {
                var childrenType = collectionInstance.GetType();
                var parentField = childrenType.GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance)
                               ?? childrenType.GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance);
                if (parentField?.GetValue(collectionInstance) is ControlTheme controlTheme) targetType = controlTheme.TargetType;
            }
            catch { }

            var style = new Style();
            try
            {
                var baseSelector = Selectors.OfType(null, targetType ?? typeof(Avalonia.Controls.Primitives.TemplatedControl));
                style.Selector = Selectors.Nesting(baseSelector);
            }
            catch { style.Selector = Selectors.Nesting(Selectors.OfType(null, typeof(Avalonia.Controls.Control))); }
            style.Setters.Add(setter);
            return style;
        }

        private static MethodInfo GetCollectionMethod(Type collectionType, string methodName, int paramCount)
        {
            var methods = collectionType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            var method = methods.FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == paramCount);
            if (method != null) return method;

            if (collectionType.IsInterface)
            {
                method = collectionType.GetMethod(methodName);
                if (method != null && method.GetParameters().Length == paramCount) return method;
                foreach (var iface in collectionType.GetInterfaces())
                {
                    method = iface.GetMethod(methodName);
                    if (method != null && method.GetParameters().Length == paramCount) return method;
                }
            }
            else
            {
                foreach (var iface in collectionType.GetInterfaces())
                {
                    if (iface.IsGenericType && (iface.GetGenericTypeDefinition() == typeof(ICollection<>) || iface.GetGenericTypeDefinition() == typeof(IList<>)))
                    {
                        try
                        {
                            var map = collectionType.GetInterfaceMap(iface);
                            for (int i = 0; i < map.InterfaceMethods.Length; i++)
                            {
                                if (map.InterfaceMethods[i].Name == methodName && map.InterfaceMethods[i].GetParameters().Length == paramCount)
                                    return map.TargetMethods[i];
                            }
                        }
                        catch { }
                    }
                }
            }
            return null;
        }
    }
}
