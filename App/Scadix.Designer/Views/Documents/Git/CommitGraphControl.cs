using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Scadix.Designer.ViewModels;

namespace Scadix.Designer.Views;

/// <summary>
/// Renders one row of the commit graph.
/// Draws:
///   - Vertical pass-through lines for active lanes
///   - The commit dot
///   - Outgoing lines toward parent columns (bottom half of row)
///   - Incoming lines from child columns (top half of row)
/// </summary>
public class CommitGraphControl : Control
{
    public static readonly StyledProperty<GitCommit?> CommitProperty =
        AvaloniaProperty.Register<CommitGraphControl, GitCommit?>(nameof(Commit));

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<CommitGraphControl, int>(nameof(MaxColumns), 1);

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<CommitGraphControl, double>(nameof(RowHeight), 26.0);

    public GitCommit? Commit
    {
        get => GetValue(CommitProperty);
        set => SetValue(CommitProperty, value);
    }

    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    private const double ColWidth  = 14.0;
    private const double DotRadius = 4.5;
    private const double LineWidth = 2.5;

    static CommitGraphControl()
    {
        CommitProperty.Changed.AddClassHandler<CommitGraphControl>((c, _) => c.InvalidateVisual());
        MaxColumnsProperty.Changed.AddClassHandler<CommitGraphControl>((c, _) =>
        {
            c.InvalidateMeasure();
            c.InvalidateVisual();
        });
    }

    public CommitGraphControl()
    {
        UseLayoutRounding = true;
    }

    protected override Size MeasureOverride(Size available)
    {
        int cols = System.Math.Max(MaxColumns, (Commit?.GraphColumn ?? 0) + 1);
        return new Size(cols * ColWidth + 4, RowHeight);
    }

    public override void Render(DrawingContext ctx)
    {
        if (Commit == null) return;

        var commit = Commit;
        double height = Bounds.Height;
        double cy = height / 2.0;
        double cx = commit.GraphColumn * ColWidth + ColWidth / 2.0;

        // 1. Pass-through vertical lines
        foreach (var edge in commit.GraphEdges.Where(e => e.FromCol == e.ToCol && e.FromCol != commit.GraphColumn))
        {
            double lx = edge.FromCol * ColWidth + ColWidth / 2.0;
            var pen = MakePen(edge.Color);
            // Draw with overlap to ensure continuity
            ctx.DrawLine(pen, new Point(lx, -1), new Point(lx, height + 1));
        }

        // 2. This commit's own lane
        int myColor = commit.GraphColumn % GraphLayoutEngine.Colors.Length;
        var myPen = MakePen(myColor);
        
        // From top to center
        ctx.DrawLine(myPen, new Point(cx, -1), new Point(cx, cy));
        
        // From center to bottom if continues
        if (commit.Parents.Count > 0)
        {
            ctx.DrawLine(myPen, new Point(cx, cy), new Point(cx, height + 1));
        }

        // 3. Outgoing merge/branch lines (bottom half)
        foreach (var edge in commit.GraphEdges.Where(e => e.FromCol != e.ToCol))
        {
            double x1 = edge.FromCol * ColWidth + ColWidth / 2.0;
            double x2 = edge.ToCol   * ColWidth + ColWidth / 2.0;
            var pen = MakePen(edge.Color);

            var geo = new StreamGeometry();
            using var gctx = geo.Open();
            gctx.BeginFigure(new Point(x1, cy), false);
            gctx.CubicBezierTo(
                new Point(x1, cy + (height - cy) * 0.6),
                new Point(x2, cy + (height - cy) * 0.4),
                new Point(x2, height + 1));
            ctx.DrawGeometry(null, pen, geo);
        }

        // 4. Incoming merge lines (top half)
        foreach (var edge in commit.GraphEdges.Where(e => e.ToCol == commit.GraphColumn && e.FromCol != commit.GraphColumn))
        {
            double x1 = edge.FromCol * ColWidth + ColWidth / 2.0;
            double x2 = cx;
            var pen = MakePen(edge.Color);

            var geo = new StreamGeometry();
            using var gctx = geo.Open();
            gctx.BeginFigure(new Point(x1, -1), false);
            gctx.CubicBezierTo(
                new Point(x1, cy * 0.4),
                new Point(x2, cy * 0.6),
                new Point(x2, cy));
            ctx.DrawGeometry(null, pen, geo);
        }

        // 5. Commit Dot
        var fill   = new SolidColorBrush(Color.Parse(GraphLayoutEngine.Colors[myColor]));
        var border = new Pen(Brushes.White, 1.5);
        ctx.DrawEllipse(fill, border, new Point(cx, cy), DotRadius, DotRadius);
    }

    private static Pen MakePen(int colorIndex)
    {
        var color = Color.Parse(GraphLayoutEngine.Colors[colorIndex % GraphLayoutEngine.Colors.Length]);
        return new Pen(new SolidColorBrush(color), LineWidth)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
    }
}
