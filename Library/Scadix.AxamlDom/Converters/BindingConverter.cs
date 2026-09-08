
using Avalonia.Data;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia Binding objects to provide design-time conversion
    /// </summary>
    public class BindingConverter : TypeConverter
    {
        #region Conversion Checks

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string)) return true;
            return base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == null) return false;

            // We can convert Binding to specific common types for design-time purposes
            return destinationType == typeof(string) ||
                   destinationType == typeof(bool) ||
                   destinationType == typeof(bool?) ||
                   destinationType == typeof(int) ||
                   destinationType == typeof(int?) ||
                   destinationType == typeof(double) ||
                   destinationType == typeof(double?) ||
                   destinationType == typeof(float) ||
                   destinationType == typeof(float?) ||
                   (destinationType.IsGenericType && destinationType.GetGenericTypeDefinition() == typeof(Nullable<>)) ||
                   base.CanConvertTo(context, destinationType);
        }

        #endregion

        #region Conversion Methods

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string stringValue)
            {
                // Simple parsing for basic binding syntax
                if (stringValue.StartsWith("{Binding") && stringValue.EndsWith("}"))
                {
                    // Extract path from simple binding syntax like "{Binding Path=PropertyName}"
                    var pathStart = stringValue.IndexOf("Path=");
                    if (pathStart > 0)
                    {
                        var pathEnd = stringValue.IndexOf(",", pathStart);
                        if (pathEnd == -1) pathEnd = stringValue.IndexOf("}", pathStart);

                        if (pathEnd >= pathStart + 5)
                        {
                            var path = stringValue.Substring(pathStart + 5, pathEnd - pathStart - 5).Trim();
                            return new Binding(path);
                        }
                    }

                    // If no path found, create empty binding
                    return new Binding();
                }
                else if (!string.IsNullOrEmpty(stringValue))
                {
                    // Treat as simple path
                    return new Binding(stringValue);
                }
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (value is BindingBase binding && destinationType != null)
            {
                // Handle nullable types first
                if (destinationType.IsGenericType && destinationType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    return null; // Return null for nullable types in design mode
                }

                // Return appropriate default values based on destination type
                return destinationType.Name switch
                {
                    nameof(String) => "[Binding]",
                    nameof(Boolean) => false,
                    nameof(Int32) => 0,
                    nameof(Double) => 0.0,
                    nameof(Single) => 0.0f,
                    _ => destinationType.IsValueType ?
                         (destinationType.IsEnum ? Enum.GetValues(destinationType).GetValue(0) : Activator.CreateInstance(destinationType)) :
                         null // For reference types, return null
                };
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion
    }
}