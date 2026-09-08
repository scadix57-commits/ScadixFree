using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Scadix.Designer.Converters
{
    public class FileIconDataConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var title = value as string ?? "";
            
            if (title.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                // C# icon
                return "M3,2 L13,2 L13,14 L3,14 Z M5,5 L5,11 L9,11 L9,9 L7,9 L7,7 L9,7 L9,5 Z"; 
            }
            if (title.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) || title.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                // XML / XAML < >
                return "M5,4 L1,8 L5,12 M11,4 L15,8 L11,12"; 
            }
            if (title.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                // JSON { }
                return "M6,2 L4,2 L4,6 L2,8 L4,10 L4,14 L6,14 M10,2 L12,2 L12,6 L14,8 L12,10 L12,14 L10,14";
            }
            
            // Default document
            return "M3,1 L9,1 L13,5 L13,15 L3,15 Z M9,1 L9,5 L13,5";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class FileIconColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var title = value as string ?? "";
            
            if (title.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return SolidColorBrush.Parse("#1E8E3E"); // C# Green
            if (title.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) || title.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                return SolidColorBrush.Parse("#F4511E"); // XAML Orange/Red
            if (title.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return SolidColorBrush.Parse("#F4B400"); // JSON Yellow
                
            return SolidColorBrush.Parse("#5F6368"); // Default Gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
