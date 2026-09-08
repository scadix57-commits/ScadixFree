using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Scadix.Designer.ViewModels.Tools;

/// <summary>Converters for the File Explorer panel.</summary>
public static class FileNodeConverters
{
    // Directory = folder gold, File = neutral grey
    public static readonly IValueConverter IsDirToColor =
        new FuncValueConverter<bool, IBrush>(isDir =>
            isDir
                ? new SolidColorBrush(Color.FromRgb(220, 180, 60))   // amber/gold
                : new SolidColorBrush(Color.FromRgb(140, 170, 210))); // soft blue-grey

    // Human-readable file size: B / KB / MB / GB
    public static readonly IValueConverter BytesToReadable =
        new FuncValueConverter<long?, string>(bytes =>
        {
            if (bytes == null) return "";
            return bytes switch
            {
                < 1024L                 => $"{bytes} B",
                < 1024L * 1024         => $"{bytes / 1024.0:F1} KB",
                < 1024L * 1024 * 1024  => $"{bytes / (1024.0 * 1024):F1} MB",
                _                      => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
            };
        });
}
