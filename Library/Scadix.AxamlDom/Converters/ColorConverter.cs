
using Avalonia.Media;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia Color type
    /// Converts between string representations and Color objects
    /// Supports: Color names, Hex colors (#RGB, #RRGGBB, #AARRGGBB), "Transparent"
    /// </summary>
    public class ColorConverter : TypeConverter
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
            if (value is string str) return Color.Parse(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is Color color)
            {
                if (color.A == 255)
                {
                    var namedColor = GetNamedColor(color);
                    if (namedColor != null) return namedColor;
                    return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                }
                return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Tries to get a named color for common colors
        /// </summary>
        private string GetNamedColor(Color color)
        {
            if (color == Colors.Transparent) return "Transparent";
            if (color == Colors.Black) return "Black";
            if (color == Colors.White) return "White";
            if (color == Colors.Red) return "Red";
            if (color == Colors.Green) return "Green";
            if (color == Colors.Blue) return "Blue";
            if (color == Colors.Yellow) return "Yellow";
            if (color == Colors.Orange) return "Orange";
            if (color == Colors.Purple) return "Purple";
            if (color == Colors.Pink) return "Pink";
            if (color == Colors.Gray) return "Gray";
            if (color == Colors.Brown) return "Brown";
            if (color == Colors.Cyan) return "Cyan";
            if (color == Colors.Magenta) return "Magenta";
            if (color == Colors.LightGray) return "LightGray";
            if (color == Colors.DarkGray) return "DarkGray";
            return null;
        }

        #endregion
    }
}
