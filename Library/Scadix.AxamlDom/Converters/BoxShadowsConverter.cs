using Avalonia.Media;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    public class BoxShadowsConverter : TypeConverter
    {
        #region Conversion Checks

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string)) return true;
            return base.CanConvertFrom(context, sourceType);
        }

        #endregion

        #region Conversion Methods

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s) return BoxShadows.Parse(s);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is BoxShadows shadows)
            {
                return shadows.ToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion
    }
}
