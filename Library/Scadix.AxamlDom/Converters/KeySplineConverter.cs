
using Avalonia.Animation;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia KeySpline type
    /// Converts between string representations (e.g., "0.4,0,0.6,1") and KeySpline objects
    /// </summary>
    public class KeySplineConverter : TypeConverter
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
            if (value is string str) return KeySpline.Parse(str, culture);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is KeySpline spline)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}",
                    spline.ControlPointX1, spline.ControlPointY1,
                    spline.ControlPointX2, spline.ControlPointY2);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion
    }
}
