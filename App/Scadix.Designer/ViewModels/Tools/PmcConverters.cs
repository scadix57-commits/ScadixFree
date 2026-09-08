using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Scadix.Designer.ViewModels.Tools;

/// <summary>Converters for the Package Manager Console panel.</summary>
public static class PmcConverters
{
    // Catppuccin Mocha palette — maps output kind to text color
    public static readonly IValueConverter KindToForeground =
        new FuncValueConverter<PmcLineKind, IBrush>(kind => kind switch
        {
            PmcLineKind.Command => new SolidColorBrush(Color.FromRgb(203, 166, 247)), // mauve
            PmcLineKind.Success => new SolidColorBrush(Color.FromRgb(166, 227, 161)), // green
            PmcLineKind.Error   => new SolidColorBrush(Color.FromRgb(243, 139, 168)), // red
            PmcLineKind.Info    => new SolidColorBrush(Color.FromRgb(137, 220, 235)), // sky
            _                   => new SolidColorBrush(Color.FromRgb(205, 214, 244)), // text
        });
}
