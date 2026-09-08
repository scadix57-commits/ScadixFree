

using Avalonia.Media;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia FontStretch type
    /// Converts between string representations and FontStretch objects
    /// Supports: UltraCondensed, ExtraCondensed, Condensed, SemiCondensed, Normal, 
    ///           SemiExpanded, Expanded, ExtraExpanded, UltraExpanded
    /// </summary>
    public class FontStretchConverter : TypeConverter
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
            if (value is string str) return ParseFontStretch(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is FontStretch fontStretch)
            {
                return FontStretchToString(fontStretch);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Parses a string into a FontStretch
        /// </summary>
        private static FontStretch ParseFontStretch(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return FontStretch.Normal;
            str = str.Trim();

            if (str.Equals("UltraCondensed", StringComparison.OrdinalIgnoreCase)) return FontStretch.UltraCondensed;
            if (str.Equals("ExtraCondensed", StringComparison.OrdinalIgnoreCase)) return FontStretch.ExtraCondensed;
            if (str.Equals("Condensed", StringComparison.OrdinalIgnoreCase)) return FontStretch.Condensed;
            if (str.Equals("SemiCondensed", StringComparison.OrdinalIgnoreCase)) return FontStretch.SemiCondensed;
            if (str.Equals("Normal", StringComparison.OrdinalIgnoreCase) || str.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return FontStretch.Normal;
            if (str.Equals("SemiExpanded", StringComparison.OrdinalIgnoreCase)) return FontStretch.SemiExpanded;
            if (str.Equals("Expanded", StringComparison.OrdinalIgnoreCase)) return FontStretch.Expanded;
            if (str.Equals("ExtraExpanded", StringComparison.OrdinalIgnoreCase)) return FontStretch.ExtraExpanded;
            if (str.Equals("UltraExpanded", StringComparison.OrdinalIgnoreCase)) return FontStretch.UltraExpanded;

            throw new FormatException($"Invalid FontStretch format: {str}. Valid values are: UltraCondensed, ExtraCondensed, Condensed, SemiCondensed, Normal, SemiExpanded, Expanded, ExtraExpanded, UltraExpanded");
        }

        /// <summary>
        /// Converts a FontStretch to its string representation
        /// </summary>
        private static string FontStretchToString(FontStretch fontStretch)
        {
            if (fontStretch == FontStretch.UltraCondensed) return "UltraCondensed";
            if (fontStretch == FontStretch.ExtraCondensed) return "ExtraCondensed";
            if (fontStretch == FontStretch.Condensed) return "Condensed";
            if (fontStretch == FontStretch.SemiCondensed) return "SemiCondensed";
            if (fontStretch == FontStretch.Normal) return "Normal";
            if (fontStretch == FontStretch.SemiExpanded) return "SemiExpanded";
            if (fontStretch == FontStretch.Expanded) return "Expanded";
            if (fontStretch == FontStretch.ExtraExpanded) return "ExtraExpanded";
            if (fontStretch == FontStretch.UltraExpanded) return "UltraExpanded";
            return fontStretch.ToString();
        }

        #endregion
    }
}
