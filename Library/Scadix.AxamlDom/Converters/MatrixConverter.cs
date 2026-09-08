
using Avalonia;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia Matrix type
    /// Converts between string representations and Matrix objects
    /// Supports: "Identity" or "M11,M12,M21,M22,OffsetX,OffsetY"
    /// </summary>
    public class MatrixConverter : TypeConverter
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
            if (value is string str) return ParseMatrix(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is Matrix matrix)
            {
                return MatrixToString(matrix);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Parses a string into a Matrix
        /// </summary>
        private static Matrix ParseMatrix(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return Matrix.Identity;
            str = str.Trim();

            if (str.Equals("Identity", StringComparison.OrdinalIgnoreCase)) return Matrix.Identity;

            var parts = str.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 6) throw new FormatException($"Invalid Matrix format: {str}. Expected format: 'M11,M12,M21,M22,OffsetX,OffsetY' or 'Identity'");

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var m11) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var m12) ||
                !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var m21) ||
                !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var m22) ||
                !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetX) ||
                !double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetY))
            {
                throw new FormatException($"Invalid Matrix format: {str}");
            }
            return new Matrix(m11, m12, m21, m22, offsetX, offsetY);
        }

        /// <summary>
        /// Converts a Matrix to its string representation
        /// </summary>
        private static string MatrixToString(Matrix matrix)
        {
            if (matrix == Matrix.Identity) return "Identity";
            var m11 = Math.Round(matrix.M11, 2);
            var m12 = Math.Round(matrix.M12, 2);
            var m21 = Math.Round(matrix.M21, 2);
            var m22 = Math.Round(matrix.M22, 2);
            var offsetX = Math.Round(matrix.M31, 2);
            var offsetY = Math.Round(matrix.M32, 2);
            return $"{m11.ToString(CultureInfo.InvariantCulture)},{m12.ToString(CultureInfo.InvariantCulture)},{m21.ToString(CultureInfo.InvariantCulture)},{m22.ToString(CultureInfo.InvariantCulture)},{offsetX.ToString(CultureInfo.InvariantCulture)},{offsetY.ToString(CultureInfo.InvariantCulture)}";
        }

        #endregion
    }
}
