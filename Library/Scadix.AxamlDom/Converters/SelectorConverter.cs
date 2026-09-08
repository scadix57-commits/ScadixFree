// Copyright (c) 2026

using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia Selector type
    /// Converts between string representations (e.g., "Button.active") and Selector objects
    /// </summary>
    public class SelectorConverter : TypeConverter
    {
        #region Conversion Checks

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        #endregion

        #region Conversion Methods

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string str)
            {
                try
                {
                    // Special handling for nesting selector "^"
                    if (str.Trim() == "^" || str.Trim().StartsWith("^:") || str.Trim().StartsWith("^ "))
                    {
                        return CreateNestingSelector(str, context);
                    }

                    // Try to find SelectorParser in multiple possible locations
                    Type selectorParserType = null;
                    var markupAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Avalonia.Markup");
                    if (markupAssembly != null) selectorParserType = FindTypeInAssembly(markupAssembly, "Avalonia.Markup.Parsers.SelectorParser");

                    if (selectorParserType == null)
                    {
                        var stylingAssembly = typeof(Avalonia.Styling.Style).Assembly;
                        selectorParserType = FindTypeInAssembly(stylingAssembly, "Avalonia.Markup.Parsers.SelectorParser");
                    }

                    if (selectorParserType == null)
                    {
                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name?.StartsWith("Avalonia") == true))
                        {
                            selectorParserType = FindTypeInAssembly(assembly, "Avalonia.Markup.Parsers.SelectorParser");
                            if (selectorParserType != null) break;
                        }
                    }

                    if (selectorParserType != null)
                    {
                        Func<string, string, Type> typeResolver = (xmlns, typeName) =>
                        {
                            if (context?.GetService(typeof(IXamlTypeResolver)) is IXamlTypeResolver xamlTypeResolver)
                            {
                                try
                                {
                                    var fullTypeName = string.IsNullOrWhiteSpace(xmlns) ? typeName : $"{xmlns}:{typeName}";
                                    return xamlTypeResolver.Resolve(fullTypeName);
                                }
                                catch { }
                            }
                            var avaloniaType = typeof(Avalonia.Controls.Control).Assembly.GetType($"Avalonia.Controls.{typeName}");
                            return avaloniaType ?? typeof(Avalonia.Controls.Control);
                        };

                        var parserInstance = Activator.CreateInstance(selectorParserType, typeResolver);
                        var parseMethod = selectorParserType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                        if (parseMethod != null) return parseMethod.Invoke(parserInstance, new object[] { str });
                    }

                    return CreateSimpleSelector(str, context);
                }
                catch (Exception ex)
                {
                    throw new NotSupportedException($"Failed to parse selector '{str}': {ex.InnerException?.Message ?? ex.Message}", ex);
                }
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is Selector selector)
            {
                return selector.ToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion

        #region Private Methods

        private Type FindTypeInAssembly(Assembly asm, string typeName)
        {
            if (asm == null) return null;
            return asm.GetType(typeName) ?? asm.GetTypes().FirstOrDefault(t => t.FullName == typeName);
        }

        private Selector CreateNestingSelector(string selectorString, ITypeDescriptorContext context)
        {
            var trimmed = selectorString.Trim();
            var suffix = trimmed.Length > 1 ? trimmed.Substring(1).Trim() : "";
            try
            {
                Selector nestingSelector = Avalonia.Styling.Selectors.Nesting(null);
                if (!string.IsNullOrEmpty(suffix))
                {
                    var current = suffix;
                    while (!string.IsNullOrEmpty(current))
                    {
                        if (current.StartsWith(":"))
                        {
                            var endIndex = current.IndexOfAny(new[] { '.', ':', ' ' }, 1);
                            var pseudoClassName = endIndex > 0 ? current.Substring(1, endIndex - 1) : current.Substring(1);
                            nestingSelector = Avalonia.Styling.Selectors.Class(nestingSelector, $":{pseudoClassName}");
                            current = endIndex > 0 ? current.Substring(endIndex) : "";
                        }
                        else if (current.StartsWith("."))
                        {
                            var endIndex = current.IndexOfAny(new[] { '.', ':', ' ' }, 1);
                            var styleClassName = endIndex > 0 ? current.Substring(1, endIndex - 1) : current.Substring(1);
                            nestingSelector = Avalonia.Styling.Selectors.Class(nestingSelector, styleClassName);
                            current = endIndex > 0 ? current.Substring(endIndex) : "";
                        }
                        else current = current.Length > 1 ? current.Substring(1) : "";
                    }
                }
                return nestingSelector;
            }
            catch { return Avalonia.Styling.Selectors.OfType(null, typeof(Avalonia.Controls.Primitives.TemplatedControl)); }
        }

        private Selector CreateSimpleSelector(string selectorString, ITypeDescriptorContext context)
        {
            if (string.IsNullOrWhiteSpace(selectorString)) throw new NotSupportedException("Empty selector string.");
            string typeName = null;
            var classes = new List<string>();
            var firstIndicatorIndex = selectorString.IndexOfAny(new[] { '.', '#', ':', ' ', '>' });

            if (firstIndicatorIndex == -1) typeName = selectorString.Trim();
            else if (firstIndicatorIndex == 0) typeName = "Control";
            else typeName = selectorString.Substring(0, firstIndicatorIndex).Trim();

            var currentPos = 0;
            while (currentPos < selectorString.Length)
            {
                var dotIndex = selectorString.IndexOf('.', currentPos);
                if (dotIndex == -1) break;
                var nextIndex = selectorString.IndexOfAny(new[] { '.', '#', ':', ' ', '>' }, dotIndex + 1);
                if (nextIndex == -1) nextIndex = selectorString.Length;
                var className = selectorString.Substring(dotIndex + 1, nextIndex - dotIndex - 1).Trim();
                if (!string.IsNullOrEmpty(className)) classes.Add(className);
                currentPos = nextIndex;
            }

            Type targetType = null;
            if (context?.GetService(typeof(IXamlTypeResolver)) is IXamlTypeResolver xamlTypeResolver)
            {
                try { targetType = xamlTypeResolver.Resolve(typeName); } catch { }
            }
            targetType ??= typeof(Avalonia.Controls.Control).Assembly.GetType($"Avalonia.Controls.{typeName}") ?? typeof(Avalonia.Controls.Control);

            var selector = Avalonia.Styling.Selectors.OfType(null, targetType);
            if (classes.Any())
            {
                var stylingAssembly = typeof(Selector).Assembly;
                var extensionsType = stylingAssembly.GetType("Avalonia.Styling.SelectorExtensions") ?? stylingAssembly.GetTypes().FirstOrDefault(t => t.Name == "SelectorExtensions");
                if (extensionsType != null)
                {
                    var classMethod = extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "Class" && m.GetParameters().Length == 2);
                    if (classMethod != null)
                    {
                        foreach (var className in classes)
                        {
                            try
                            {
                                if (classMethod.Invoke(null, new object[] { selector, className }) is Selector nextSelector) selector = nextSelector;
                            }
                            catch { }
                        }
                    }
                }
            }
            return selector;
        }

        #endregion
    }
}
