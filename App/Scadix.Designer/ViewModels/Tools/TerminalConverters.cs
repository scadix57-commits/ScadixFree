using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Scadix.Designer.ViewModels.Tools;

/// <summary>
/// A static hub of lightweight value converters specifically for the Terminal panel.
/// Keeping them in the ViewModel namespace avoids the need for a separate Converters file
/// and keeps terminal logic self-contained.
/// </summary>
public static class TerminalConverters
{
    // ── Color-code output by stream kind ─────────────────────────────────────

    public static readonly IValueConverter KindToForeground = new FuncValueConverter<TerminalLineKind, IBrush>(kind =>
        kind switch
        {
            TerminalLineKind.StdErr => new SolidColorBrush(Color.FromRgb(255, 100, 100)),   // red-ish
            TerminalLineKind.System => new SolidColorBrush(Color.FromRgb(100, 180, 255)),   // blue
            TerminalLineKind.Error  => new SolidColorBrush(Color.FromRgb(255, 80,  80)),    // bright red
            _                      => new SolidColorBrush(Color.FromRgb(212, 212, 212)),    // stdout = light grey
        });

    // ── Indicator dot: green = alive, grey = dead ─────────────────────────────

    public static readonly IValueConverter AliveToColor = new FuncValueConverter<bool, Color>(alive =>
        alive ? Color.FromRgb(87, 197, 132) : Color.FromRgb(100, 100, 100));
}
