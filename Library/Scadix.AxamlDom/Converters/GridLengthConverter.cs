

using Avalonia.Controls;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia GridLength type
    /// Converts between string representations and GridLength objects
    /// Supports: "Auto", "*", "2*", "100" (pixels)
    /// </summary>
    public class GridLengthConverter : TypeConverter
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
            if (value is string str) return ParseGridLength(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is GridLength gridLength)
            {
                return GridLengthToString(gridLength);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Parses a string into a GridLength
        /// </summary>
        private static GridLength ParseGridLength(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return new GridLength(0, GridUnitType.Pixel);
            str = str.Trim();

            if (str.Equals("Auto", StringComparison.OrdinalIgnoreCase)) return GridLength.Auto;

            if (str.EndsWith("*"))
            {
                if (str == "*") return new GridLength(1, GridUnitType.Star);
                var valueStr = str.Substring(0, str.Length - 1).Trim();
                if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var starValue))
                    return new GridLength(starValue, GridUnitType.Star);
                throw new FormatException($"Invalid star value: {str}");
            }

            var pixelStr = str;
            if (str.EndsWith("px", StringComparison.OrdinalIgnoreCase)) pixelStr = str.Substring(0, str.Length - 2).Trim();
            if (double.TryParse(pixelStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixelValue))
                return new GridLength(pixelValue, GridUnitType.Pixel);

            throw new FormatException($"Invalid GridLength format: {str}");
        }

        /// <summary>
        /// Converts a GridLength to its string representation
        /// </summary>
        private static string GridLengthToString(GridLength gridLength)
        {
            switch (gridLength.GridUnitType)
            {
                case GridUnitType.Auto: return "Auto";
                case GridUnitType.Star:
                    var starValue = Math.Round(gridLength.Value, 2);
                    if (starValue == 1.0) return "*";
                    return $"{starValue.ToString(CultureInfo.InvariantCulture)}*";
                case GridUnitType.Pixel:
                    var pixelValue = Math.Round(gridLength.Value, 2);
                    return pixelValue.ToString(CultureInfo.InvariantCulture);
                default:
                    return gridLength.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        #endregion
    }
}
