
using Avalonia.Media;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia FontWeight type
    /// Converts between string representations and FontWeight objects
    /// Supports: Named weights (Normal, Bold, etc.) and numeric values (100-900)
    /// </summary>
    public class FontWeightConverter : TypeConverter
    {
        #region Conversion Checks

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || sourceType == typeof(int) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        #endregion

        #region Conversion Methods

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string str) return ParseFontWeight(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is FontWeight fontWeight)
            {
                return FontWeightToString(fontWeight);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Parses a string into a FontWeight
        /// </summary>
        private static FontWeight ParseFontWeight(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return FontWeight.Normal;
            str = str.Trim();

            // Try named weights first
            if (str.Equals("Thin", StringComparison.OrdinalIgnoreCase)) return FontWeight.Thin;
            if (str.Equals("ExtraLight", StringComparison.OrdinalIgnoreCase) || str.Equals("UltraLight", StringComparison.OrdinalIgnoreCase)) return FontWeight.ExtraLight;
            if (str.Equals("Light", StringComparison.OrdinalIgnoreCase)) return FontWeight.Light;
            if (str.Equals("Normal", StringComparison.OrdinalIgnoreCase) || str.Equals("Regular", StringComparison.OrdinalIgnoreCase)) return FontWeight.Normal;
            if (str.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return FontWeight.Medium;
            if (str.Equals("SemiBold", StringComparison.OrdinalIgnoreCase) || str.Equals("DemiBold", StringComparison.OrdinalIgnoreCase)) return FontWeight.SemiBold;
            if (str.Equals("Bold", StringComparison.OrdinalIgnoreCase)) return FontWeight.Bold;
            if (str.Equals("ExtraBold", StringComparison.OrdinalIgnoreCase) || str.Equals("UltraBold", StringComparison.OrdinalIgnoreCase)) return FontWeight.ExtraBold;
            if (str.Equals("Black", StringComparison.OrdinalIgnoreCase) || str.Equals("Heavy", StringComparison.OrdinalIgnoreCase)) return FontWeight.Black;
            if (str.Equals("ExtraBlack", StringComparison.OrdinalIgnoreCase) || str.Equals("UltraBlack", StringComparison.OrdinalIgnoreCase)) return FontWeight.ExtraBlack;

            // Try numeric value (100-900)
            if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight))
            {
                if (weight <= 100) return FontWeight.Thin;
                if (weight <= 200) return FontWeight.ExtraLight;
                if (weight <= 300) return FontWeight.Light;
                if (weight <= 400) return FontWeight.Normal;
                if (weight <= 500) return FontWeight.Medium;
                if (weight <= 600) return FontWeight.SemiBold;
                if (weight <= 700) return FontWeight.Bold;
                if (weight <= 800) return FontWeight.ExtraBold;
                if (weight <= 900) return FontWeight.Black;
                return FontWeight.ExtraBlack;
            }

            throw new FormatException($"Invalid FontWeight format: {str}");
        }

        /// <summary>
        /// Converts a FontWeight to its string representation
        /// </summary>
        private static string FontWeightToString(FontWeight fontWeight)
        {
            if (fontWeight == FontWeight.Thin) return "Thin";
            if (fontWeight == FontWeight.ExtraLight) return "ExtraLight";
            if (fontWeight == FontWeight.Light) return "Light";
            if (fontWeight == FontWeight.Normal) return "Normal";
            if (fontWeight == FontWeight.Medium) return "Medium";
            if (fontWeight == FontWeight.SemiBold) return "SemiBold";
            if (fontWeight == FontWeight.Bold) return "Bold";
            if (fontWeight == FontWeight.ExtraBold) return "ExtraBold";
            if (fontWeight == FontWeight.Black) return "Black";
            if (fontWeight == FontWeight.ExtraBlack) return "ExtraBlack";
            return fontWeight.ToString();
        }

        #endregion
    }
}
