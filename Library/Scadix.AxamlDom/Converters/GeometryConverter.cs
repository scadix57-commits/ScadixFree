

using Avalonia.Media;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia Geometry type
    /// Converts between string representations and Geometry objects
    /// Supports path data strings (e.g., "M 0,0 L 100,100")
    /// </summary>
    public class GeometryConverter : TypeConverter
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
            if (value is string str) return ParseGeometry(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is Geometry geometry)
            {
                return GeometryToString(geometry);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Parses a string into a Geometry using Avalonia's built-in parser
        /// </summary>
        private static Geometry ParseGeometry(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return null;
            try { return Geometry.Parse(str); }
            catch (Exception ex) { throw new FormatException($"Invalid Geometry format: {str}", ex); }
        }

        /// <summary>
        /// Converts a Geometry to its string representation
        /// </summary>
        private static string GeometryToString(Geometry geometry)
        {
            if (geometry == null) return string.Empty;
            return geometry.ToString();
        }

        #endregion
    }
}
