

using Avalonia;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia Thickness type
    /// </summary>
    public class ThicknessConverter : TypeConverter
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
            if (value is string str) return Thickness.Parse(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is Thickness thickness)
            {
                var left = Math.Round(thickness.Left, 2);
                var top = Math.Round(thickness.Top, 2);
                var right = Math.Round(thickness.Right, 2);
                var bottom = Math.Round(thickness.Bottom, 2);

                if (left == right && left == top && left == bottom) return left.ToString(CultureInfo.InvariantCulture);
                if (left == right && top == bottom) return $"{left.ToString(CultureInfo.InvariantCulture)},{top.ToString(CultureInfo.InvariantCulture)}";
                return $"{left.ToString(CultureInfo.InvariantCulture)},{top.ToString(CultureInfo.InvariantCulture)},{right.ToString(CultureInfo.InvariantCulture)},{bottom.ToString(CultureInfo.InvariantCulture)}";
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion
    }
}
