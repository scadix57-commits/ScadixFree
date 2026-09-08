

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner
{
	public class IntFromEnumConverter : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly IntFromEnumConverter Instance = new IntFromEnumConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (int)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Enum.ToObject(targetType, (int)value);
		}
	}

	public class HiddenWhenFalse : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly HiddenWhenFalse Instance = new HiddenWhenFalse();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// Avalonia doesn't have false, use Collapsed instead
			return (bool)value ? true : false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class CollapsedWhenFalse : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly CollapsedWhenFalse Instance = new CollapsedWhenFalse();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (bool)value ? true : false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class LevelConverter : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly LevelConverter Instance = new LevelConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return new Thickness(2 + 14 * (int)value, 0, 0, 0);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class CollapsedWhenZero : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly CollapsedWhenZero Instance = new CollapsedWhenZero();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || (value is int && (int)value == 0)) {
				return false;
			}
			return true;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class CollapsedWhenNotNull : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly CollapsedWhenNotNull Instance = new CollapsedWhenNotNull();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value != null)
			{
				return false;
			}
			return true;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class CollapsedWhenNull : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly CollapsedWhenNull Instance = new CollapsedWhenNull();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value != null)
			{
				return true;
			}
			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class FalseWhenNull : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly FalseWhenNull Instance = new FalseWhenNull();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value != null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class BoldWhenTrue : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly BoldWhenTrue Instance = new BoldWhenTrue();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (bool)value ? FontWeight.Bold : FontWeight.Normal;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	// Boxed int throw exception without converter (wpf bug?)
	public class DummyConverter : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly DummyConverter Instance = new DummyConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}
	}

	public class ControlToRealWidthConverter : IMultiValueConverter
	{
		public static readonly ControlToRealWidthConverter Instance = new ControlToRealWidthConverter();

		public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			if (values?.Count > 0 && values[0] is Control ctrl)
				return Math.Round(PlacementOperation.GetRealElementSize(ctrl).Width).ToString(culture);
			return "0";
		}
	}

	public class ControlToRealHeightConverter : IMultiValueConverter
	{
		public static readonly ControlToRealHeightConverter Instance = new ControlToRealHeightConverter();

		public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			if (values?.Count > 0 && values[0] is Control ctrl)
				return Math.Round(PlacementOperation.GetRealElementSize(ctrl).Height).ToString(culture);
			return "0";
		}
	}

	public class FormatDoubleConverter : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly FormatDoubleConverter Instance=new FormatDoubleConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Math.Round((double)value);
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class DoubleOffsetConverter : IValueConverter
	{
		public double Offset { get; set; }
		
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (double)value + Offset;
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (double)value - Offset;
		}
	}

	public class BlackWhenTrue : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly BlackWhenTrue Instance = new BlackWhenTrue();

		private Brush black;

		public BlackWhenTrue()
		{
			black = new SolidColorBrush(Colors.Black);
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (bool)value ? black : null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
	
	public class EnumBoolean : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly EnumBoolean Instance = new EnumBoolean();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			string parameterString = parameter as string;
			if (parameterString == null)
				return AvaloniaProperty.UnsetValue;

			if (Enum.IsDefined(value.GetType(), value) == false)
				return AvaloniaProperty.UnsetValue;

			object parameterValue = Enum.Parse(value.GetType(), parameterString);

			return parameterValue.Equals(value);
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			string parameterString = parameter as string;
			if (parameterString == null)
				return AvaloniaProperty.UnsetValue;

			return Enum.Parse(targetType, parameterString);
		}
	}
	
	public class EnumVisibility : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly EnumVisibility Instance = new EnumVisibility();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			string parameterString = parameter as string;
			if (parameterString == null)
				return AvaloniaProperty.UnsetValue;

			if (Enum.IsDefined(value.GetType(), value) == false)
				return AvaloniaProperty.UnsetValue;

			object parameterValue = Enum.Parse(value.GetType(), parameterString);

			return parameterValue.Equals(value) ? true : false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			string parameterString = parameter as string;
			if (parameterString == null)
				return AvaloniaProperty.UnsetValue;

			return Enum.Parse(targetType, parameterString);
		}
	}

	public class EnumCollapsed : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly EnumCollapsed Instance = new EnumCollapsed();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			string parameterString = parameter as string;
			if (parameterString == null || value == null)
				return AvaloniaProperty.UnsetValue;

			if (!value.GetType().IsEnum)
				return AvaloniaProperty.UnsetValue;

			if (Enum.IsDefined(value.GetType(), value) == false)
				return AvaloniaProperty.UnsetValue;

			object parameterValue = Enum.Parse(value.GetType(), parameterString);

			return parameterValue.Equals(value) ? false : true;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			string parameterString = parameter as string;
			if (parameterString == null)
				return AvaloniaProperty.UnsetValue;

			return Enum.Parse(targetType, parameterString);
		}
	}

	public class InvertedZoomConverter : IValueConverter
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "converter is immutable")]
		public static readonly InvertedZoomConverter Instance = new InvertedZoomConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return 1.0 / ((double)value);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return 1.0 / ((double)value);
		}
	}
}
