using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia CompiledBindingPath type
    /// </summary>
    public class CompiledBindingPathConverter : TypeConverter
    {
        #region Fields

        private readonly Type _compiledBindingPathType;

        #endregion

        #region Constructors

        public CompiledBindingPathConverter(Type compiledBindingPathType)
        {
            _compiledBindingPathType = compiledBindingPathType;
        }

        #endregion

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
                try
                {
                    // DESIGNER NOTE: CompiledBindingPath in Avalonia 11 is strictly for compiled code.
                    // Returning null here prevents a crash; the actual binding will be handled 
                    // as a regular reflection-based Binding by the designer's internal logic.
                    return null;
                }
                catch { return null; }
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value != null && value.GetType() == _compiledBindingPathType)
            {
                return value.ToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion
    }
}
