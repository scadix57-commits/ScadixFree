using Avalonia;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia CornerRadius type
    /// </summary>
    public class CornerRadiusConverter : TypeConverter
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
            if (value is string str) return CornerRadius.Parse(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is CornerRadius cornerRadius)
            {
                if (cornerRadius.TopLeft == cornerRadius.TopRight &&
                    cornerRadius.TopLeft == cornerRadius.BottomLeft &&
                    cornerRadius.TopLeft == cornerRadius.BottomRight)
                {
                    return cornerRadius.TopLeft.ToString(CultureInfo.InvariantCulture);
                }
                return $"{cornerRadius.TopLeft.ToString(CultureInfo.InvariantCulture)},{cornerRadius.TopRight.ToString(CultureInfo.InvariantCulture)},{cornerRadius.BottomRight.ToString(CultureInfo.InvariantCulture)},{cornerRadius.BottomLeft.ToString(CultureInfo.InvariantCulture)}";
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion
    }
}
