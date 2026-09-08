using Avalonia.Controls.Documents;
using System;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    public class InlineConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string str)
            {
                return new Run(str);
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
