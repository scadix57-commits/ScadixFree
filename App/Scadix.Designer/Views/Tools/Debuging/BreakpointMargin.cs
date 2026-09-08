using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using System;
using System.Collections.Generic;

namespace Scadix.Designer.Views.Tools;

/// <summary>
/// Left-gutter margin — Visual Studio style breakpoints:
///   • Red circle   = breakpoint set
///   • Yellow arrow = current execution line (debugger paused)
///   • Hover ghost  = where a breakpoint would be placed
///   • F9 toggles breakpoint on the caret line
/// </summary>
public class BreakpointMargin : AbstractMargin
{
    // ── Brushes ───────────────────────────────────────────────────────────
    private static readonly IBrush BpFill         = new SolidColorBrush(Color.Parse("#E51400"));
    private static readonly IBrush BpHoverFill    = new SolidColorBrush(Color.Parse("#FF6B6B"));
    private static readonly IBrush BpHitFill      = new SolidColorBrush(Color.Parse("#FFD700"));
    private static readonly IPen   BpHitPen       = new Pen(new SolidColorBrush(Color.Parse("#B8860B")), 1.5);
    private static readonly IBrush BpDisabledFill = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IPen   BpDisabledPen  = new Pen(new SolidColorBrush(Color.Parse("#E51400")), 1.5);
    private static readonly IBrush ArrowFill      = new SolidColorBrush(Color.Parse("#FFD700"));
    private static readonly IPen   ArrowPen       = new Pen(new SolidColorBrush(Color.Parse("#B8860B")), 1);
    private static readonly IBrush BgBrush        = new SolidColorBrush(Color.Parse("#F0F0F0"));
    private static readonly IPen   BorderPen      = new Pen(Brushes.LightGray, 1);
    private static readonly IBrush GhostFill      = new SolidColorBrush(Color.FromArgb(60, 229, 20, 0));

    // ── State ─────────────────────────────────────────────────────────────
    private readonly HashSet<int> _breakpoints         = new();
    private readonly HashSet<int> _disabledBreakpoints = new();
    private int _currentExecutionLine = -1;
    private int _hoverLine            = -1;

    // ── Events ────────────────────────────────────────────────────────────
    /// <summary>Fired when user clicks gutter or presses F9. Host syncs with BreakpointService.</summary>
    public event EventHandler<int>? BreakpointToggled;

    // ── Public API ────────────────────────────────────────────────────────

    public IReadOnlyCollection<int> Breakpoints => _breakpoints;

    public void SetBreakpoints(IEnumerable<int> lines)
    {
        _breakpoints.Clear();
        foreach (var l in lines) _breakpoints.Add(l);
        InvalidateVisual();
    }

    public void SyncBreakpoint(int line, bool active)
    {
        if (active) _breakpoints.Add(line);
        else        _breakpoints.Remove(line);
        InvalidateVisual();
    }

    /// <summary>
    /// Highlights the current execution line with a yellow arrow.
    /// Also scrolls the editor to that line. Pass -1 to clear.
    /// </summary>
    public void SetCurrentExecutionLine(int line)
    {
        _currentExecutionLine = line;
        InvalidateVisual();
        TextArea?.TextView?.InvalidateLayer(KnownLayer.Background);

        if (line > 0 && TextArea?.TextView?.Document != null)
        {
            try
            {
                var doc = TextArea.TextView.Document;
                if (line <= doc.LineCount)
                {
                    TextArea.Caret.Offset = doc.GetLineByNumber(line).Offset;
                    TextArea.Caret.BringCaretToView();
                }
            }
            catch { }
        }
    }

    public int CurrentExecutionLine => _currentExecutionLine;

    public void ClearBreakpoints()
    {
        _breakpoints.Clear();
        InvalidateVisual();
    }

    /// <summary>Toggle breakpoint on a specific line (called from F9 or gutter click).</summary>
    public void ToggleBreakpoint(int line)
    {
        // لا نُعدّل _breakpoints هنا — BreakpointService هو المصدر الوحيد للحقيقة.
        // نُطلق الـ event فقط، والـ host سيُحدّث BreakpointService،
        // الذي سيُطلق BreakpointsChanged، الذي سيستدعي SetBreakpoints.
        BreakpointToggled?.Invoke(this, line);
    }

    /// <summary>Toggle breakpoint on the caret's current line.</summary>
    public void ToggleBreakpointAtCaret()
    {
        if (TextArea?.Document == null) return;
        var line = TextArea.Document.GetLineByOffset(TextArea.Caret.Offset).LineNumber;
        ToggleBreakpoint(line);
    }

    // ── Pointer events ────────────────────────────────────────────────────

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        base.OnPointerEntered(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        Cursor = Cursor.Default;
        _hoverLine = -1;
        InvalidateVisual();
        base.OnPointerExited(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (TextArea?.TextView == null) return;
        var line = HitTestLine(e.GetPosition(TextArea.TextView));
        if (line != _hoverLine)
        {
            _hoverLine = line;
            InvalidateVisual();
        }
        base.OnPointerMoved(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (TextArea?.TextView == null) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var line = HitTestLine(e.GetPosition(TextArea.TextView));
            if (line > 0) ToggleBreakpoint(line);
            e.Handled = true;
        }
        base.OnPointerPressed(e);
    }

    private int HitTestLine(Point posInTextView)
    {
        var tv = TextArea!.TextView;
        var vl = tv.GetVisualLineFromVisualTop(posInTextView.Y + tv.VerticalOffset);
        return vl?.FirstDocumentLine.LineNumber ?? -1;
    }

    // ── TextView wiring ───────────────────────────────────────────────────

    protected override void OnTextViewChanged(TextView? oldTv, TextView? newTv)
    {
        if (oldTv != null) oldTv.VisualLinesChanged -= OnVisualLinesChanged;
        base.OnTextViewChanged(oldTv, newTv);
        if (newTv != null) newTv.VisualLinesChanged += OnVisualLinesChanged;
        InvalidateVisual();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    // ── Rendering ─────────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        var tv = TextArea?.TextView;
        if (tv == null || !tv.VisualLinesValid) return;

        ctx.DrawRectangle(BgBrush, null, new Rect(Bounds.Size));
        ctx.DrawLine(BorderPen,
            new Point(Bounds.Width - 0.5, 0),
            new Point(Bounds.Width - 0.5, Bounds.Height));

        double cx = Bounds.Width / 2.0;
        double r  = Math.Min(cx - 2, 7);

        foreach (var vl in tv.VisualLines)
        {
            int    ln = vl.FirstDocumentLine.LineNumber;
            double cy = vl.GetTextLineVisualYPosition(vl.TextLines[0], VisualYPosition.TextMiddle)
                        - tv.VerticalOffset;

            // Yellow arrow for current execution line
            if (ln == _currentExecutionLine)
                DrawArrow(ctx, cx, cy, r);

            // Breakpoint circle
            if (_breakpoints.Contains(ln))
            {
                if (_disabledBreakpoints.Contains(ln))
                    ctx.DrawEllipse(BpDisabledFill, BpDisabledPen, new Point(cx, cy), r, r);
                else if (ln == _currentExecutionLine)
                    ctx.DrawEllipse(BpHitFill, BpHitPen, new Point(cx, cy), r, r);
                else
                {
                    var fill = ln == _hoverLine ? BpHoverFill : BpFill;
                    ctx.DrawEllipse(fill, null, new Point(cx, cy), r, r);
                }
            }
            else if (ln == _hoverLine)
            {
                ctx.DrawEllipse(GhostFill, null, new Point(cx, cy), r, r);
            }
        }
    }

    private static void DrawArrow(DrawingContext ctx, double cx, double cy, double r)
    {
        var geo = new StreamGeometry();
        using (var sgc = geo.Open())
        {
            sgc.BeginFigure(new Point(cx - r + 2, cy - r + 2), true);
            sgc.LineTo(new Point(cx + r - 1, cy));
            sgc.LineTo(new Point(cx - r + 2, cy + r - 2));
            sgc.EndFigure(true);
        }
        ctx.DrawGeometry(ArrowFill, ArrowPen, geo);
    }

    protected override Size MeasureOverride(Size availableSize) => new Size(20, 0);
}

// ── Legacy event args — kept for compatibility ────────────────────────────────
public class BreakpointEventArgs : EventArgs
{
    public int    Line     { get; }
    public string? FilePath { get; set; }
    public BreakpointEventArgs(int line, string? filePath = null) { Line = line; FilePath = filePath; }
}
