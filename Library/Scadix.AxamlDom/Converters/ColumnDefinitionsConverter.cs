using Avalonia.Controls;
using System;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    public class ColumnDefinitionsConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string str) return ColumnDefinitions.Parse(str);
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is ColumnDefinitions columnDefinitions)
            {
                return columnDefinitions.ToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
