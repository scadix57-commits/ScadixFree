using Avalonia.Media.Imaging;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// Specialized TypeConverter for Avalonia's WindowIcon
    /// </summary>
    public class IconConverter : TypeConverter
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
            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str)) return null;

                try
                {
                    var stream = BitmapConverter.OpenStreamInternal(str, context);
                    if (stream != null)
                    {
                        using (stream)
                        {
                            return new Bitmap(stream);
                        }
                    }
                }
                catch { return null; }
                return null;
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                if (value == null) return string.Empty;
                return value.ToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion
    }
}
