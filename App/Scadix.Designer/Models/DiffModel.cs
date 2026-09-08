using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Scadix.Designer.Models;

public class DiffLine : ObservableObject
{
    public string  Text       { get; set; } = "";
    public string  LineNumber { get; set; } = "";
    public IBrush  Background { get; set; } = Brushes.Transparent;
    public IBrush  Foreground { get; set; } = Brushes.Transparent;

    public enum Kind { Normal, Added, Removed, Empty }

    // ── Shared Brushes ───────────────────────────────────────────────────────
    public static readonly IBrush AddedBg    = new SolidColorBrush(Color.FromArgb(60,  80, 200,  80));
    public static readonly IBrush RemovedBg  = new SolidColorBrush(Color.FromArgb(60, 200,  60,  60));
    public static readonly IBrush AddedFg    = new SolidColorBrush(Color.Parse("#5DA656"));
    public static readonly IBrush RemovedFg  = new SolidColorBrush(Color.Parse("#E06C75"));
    public static readonly IBrush NormalFg   = new SolidColorBrush(Color.Parse("#CCCCCC"));
    public static readonly IBrush EmptyBg;

    static DiffLine()
    {
        // Create a diagonal hatch pattern for empty areas
        var geometry = new GeometryGroup();
        geometry.Children.Add(new LineGeometry(new Avalonia.Point(0, 5), new Avalonia.Point(5, 0)));
        var drawing = new GeometryDrawing
        {
            Geometry = geometry,
            Pen = new Pen(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), 1)
        };
        EmptyBg = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            DestinationRect = new Avalonia.RelativeRect(0, 0, 5, 5, Avalonia.RelativeUnit.Absolute),
            Opacity = 0.5
        };
    }
}
