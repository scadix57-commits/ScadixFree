// Copyright (c) 2026 MyDesigner Team
// Safe MultiValueConverter for handling MultiBinding scenarios

using Avalonia;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// A safe implementation of IMultiValueConverter that handles null values gracefully
    /// and provides automatic string formatting support.
    /// </summary>
    public class SafeMultiValueConverter : IMultiValueConverter
    {
        #region Methods

        /// <summary>
        /// Converts multiple source values to a single target value.
        /// Handles null values by replacing them with "[null]" placeholder.
        /// </summary>
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Count == 0) return AvaloniaProperty.UnsetValue;

            // Handle null values gracefully by replacing with placeholder
            var safeValues = values.Select(v => v ?? "[null]").ToArray();

            // If parameter is a format string, use it
            if (parameter is string format && !string.IsNullOrEmpty(format))
            {
                try
                {
                    return string.Format(culture ?? CultureInfo.CurrentCulture, format, safeValues);
                }
                catch (FormatException ex)
                {
                    return $"[Format Error: {ex.Message}]";
                }
            }

            // Default behavior: Join with comma separator
            return string.Join(", ", safeValues);
        }

        /// <summary>
        /// ConvertBack is not supported for SafeMultiValueConverter.
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("ConvertBack is not supported for SafeMultiValueConverter. Use one-way bindings only.");
        }

        #endregion
    }
}
