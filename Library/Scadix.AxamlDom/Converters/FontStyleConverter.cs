

using Avalonia.Media;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia FontStyle type
    /// Converts between string representations and FontStyle objects
    /// Supports: Normal, Italic, Oblique
    /// </summary>
    public class FontStyleConverter : TypeConverter
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
            if (value is string str) return ParseFontStyle(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is FontStyle fontStyle)
            {
                return FontStyleToString(fontStyle);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Parses a string into a FontStyle
        /// </summary>
        private static FontStyle ParseFontStyle(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return FontStyle.Normal;
            str = str.Trim();

            if (str.Equals("Normal", StringComparison.OrdinalIgnoreCase)) return FontStyle.Normal;
            if (str.Equals("Italic", StringComparison.OrdinalIgnoreCase)) return FontStyle.Italic;
            if (str.Equals("Oblique", StringComparison.OrdinalIgnoreCase)) return FontStyle.Oblique;

            throw new FormatException($"Invalid FontStyle format: {str}. Valid values are: Normal, Italic, Oblique");
        }

        /// <summary>
        /// Converts a FontStyle to its string representation
        /// </summary>
        private static string FontStyleToString(FontStyle fontStyle)
        {
            if (fontStyle == FontStyle.Normal) return "Normal";
            if (fontStyle == FontStyle.Italic) return "Italic";
            if (fontStyle == FontStyle.Oblique) return "Oblique";
            return fontStyle.ToString();
        }

        #endregion
    }
}
