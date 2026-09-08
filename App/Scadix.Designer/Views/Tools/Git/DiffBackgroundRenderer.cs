using System.Collections.ObjectModel;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Scadix.Designer.Models;

namespace Scadix.Designer.Views.Tools;

/// <summary>
/// Draws colored backgrounds on diff lines (red = removed, green = added).
/// </summary>
public class DiffBackgroundRenderer : IBackgroundRenderer
{
    private static readonly IBrush AddedBg   = DiffLine.AddedBg;
    private static readonly IBrush RemovedBg = DiffLine.RemovedBg;
    private static readonly IBrush EmptyBg   = DiffLine.EmptyBg;

    private readonly TextEditor                  _editor;
    private readonly ObservableCollection<DiffLine> _lines;

    public KnownLayer Layer => KnownLayer.Background;

    public DiffBackgroundRenderer(TextEditor editor, ObservableCollection<DiffLine> lines)
    {
        _editor = editor;
        _lines  = lines;
    }

    public void Draw(TextView textView, DrawingContext ctx)
    {
        if (_editor.Document == null) return;

        foreach (var vl in textView.VisualLines)
        {
            int lineNum = vl.FirstDocumentLine.LineNumber; // 1-based
            if (lineNum < 1 || lineNum > _lines.Count) continue;

            var diffLine = _lines[lineNum - 1];

            IBrush? bg = null;
            if (diffLine.Background == Brushes.Transparent) continue;

            // Match by kind or color
            if (diffLine.Background is SolidColorBrush scb)
            {
                if (scb.Color.A == 60 || scb.Color.A == 50) // Matches our diff colors
                    bg = scb;
            }
            
            if (bg == null) bg = diffLine.Background;

            double y      = vl.GetTextLineVisualYPosition(vl.TextLines[0], VisualYPosition.LineTop)
                            - textView.VerticalOffset;
            double height = vl.Height;
            double width  = textView.Bounds.Width;

            ctx.DrawRectangle(bg, null, new Avalonia.Rect(0, y, width, height));
        }
    }
}
