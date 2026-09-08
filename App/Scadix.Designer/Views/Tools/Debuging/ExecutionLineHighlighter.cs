using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace Scadix.Designer.Views.Tools;

/// <summary>
/// Draws a semi-transparent yellow background on the current execution line.
/// Works together with BreakpointMargin's yellow arrow.
/// </summary>
public class ExecutionLineHighlighter : IBackgroundRenderer
{
    private static readonly IBrush YellowBg =
        new SolidColorBrush(Color.FromArgb(80, 255, 216, 55));   // semi-transparent yellow

    private static readonly IBrush YellowBorder =
        new SolidColorBrush(Color.Parse("#FFD700"));

    public KnownLayer Layer => KnownLayer.Background;

    private int _line = -1;

    public void SetLine(int line) => _line = line;

    public void Draw(TextView textView, Avalonia.Media.DrawingContext drawingContext)
    {
        if (_line <= 0 || textView.Document == null) return;
        if (_line > textView.Document.LineCount) return;

        foreach (var vl in textView.VisualLines)
        {
            if (vl.FirstDocumentLine.LineNumber != _line) continue;

            double y      = vl.GetTextLineVisualYPosition(vl.TextLines[0], VisualYPosition.LineTop)
                            - textView.VerticalOffset;
            double height = vl.Height;
            double width  = textView.Bounds.Width;

            var rect = new Avalonia.Rect(0, y, width, height);
            drawingContext.DrawRectangle(YellowBg, new Pen(YellowBorder, 1), rect);
            break;
        }
    }
}
