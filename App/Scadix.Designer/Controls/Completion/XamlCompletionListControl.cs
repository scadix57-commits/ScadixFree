using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Scadix.Designer.Controls.Completion;

/// <summary>
/// عنصر التحكم الذي يرسم قائمة الإكمال — مطابقة تامة لـ CSharpEditor.CompletionListControl
/// </summary>
internal class XamlCompletionListControl : Control
{
    private ImmutableList<XamlCompletionItem> _items = ImmutableList<XamlCompletionItem>.Empty;

    public ImmutableList<XamlCompletionItem> Items
    {
        get => _items;
        set
        {
            _items = value;
            Hidden = new bool[value.Count];
            ComputeMaxTextWidth();
        }
    }

    public bool[] Hidden { get; private set; } = Array.Empty<bool>();
    public string? Filter { get; set; }
    public int SelectedIndex { get; set; } = -1;
    public double MaxTextWidth { get; private set; } = 200;

    private readonly XamlCompletionWindow _owner;

    private static readonly FontFamily InterFont = new("avares://Avalonia.Fonts.Inter/Assets#Inter");
    private const double FontSz = 14.0;
    private const double RowH   = 20.0;

    private static readonly IBrush BgBrush  = new SolidColorBrush(Color.FromRgb(247, 249, 254));
    private static readonly IBrush SelBrush = new SolidColorBrush(Color.FromRgb(196, 213, 255));

    public XamlCompletionListControl(XamlCompletionWindow owner)
    {
        _owner = owner;
        DoubleTapped += (_, _) => _owner.Commit();
    }


    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(MaxTextWidth, Hidden.Where(a => !a).Count() * RowH);
    }

    private void ComputeMaxTextWidth()
    {
        var face = new Typeface(InterFont);
        double max = 160;
        foreach (var item in _items)
        {
            var ft = new Avalonia.Media.FormattedText(item.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, FontSz, Brushes.Black);
            max = Math.Max(max, ft.Width);
        }
        MaxTextWidth = max;
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(BgBrush, new Rect(0, 0, Bounds.Width, Bounds.Height));

        var face = new Typeface(InterFont);
        var boldFace = new Typeface(InterFont, weight: FontWeight.Bold);

        int rowIdx = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            if (Hidden[i]) continue;

            double y = rowIdx * RowH;

            // 1. Selection Highlight (Starts at x=20 as in CompletionListControl)
            if (i == SelectedIndex)
            {
                ctx.FillRectangle(SelBrush, new Rect(20, y, MaxTextWidth + 2 + 5, RowH));
            }

            // 2. Icon (x=2, y+2, size 16x16)
            var icon = XamlCompletionIcons.Get(_items[i].Kind);
            if (icon != null)
            {
                ctx.DrawImage(icon, new Rect(2, y + 2, 16, 16));
            }

            // 3. Text (x=22)
            double x = 22;
            string filter = Filter ?? "";
            string txt = _items[i].Text;

            if (!string.IsNullOrEmpty(filter) && txt.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                var fmtBold = new Avalonia.Media.FormattedText(txt.Substring(0, filter.Length), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, boldFace, FontSz, Brushes.Black);
                ctx.DrawText(fmtBold, new Point(x, y));
                x += fmtBold.Width;

                var fmtNorm = new Avalonia.Media.FormattedText(txt.Substring(filter.Length), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, FontSz, Brushes.Black);
                ctx.DrawText(fmtNorm, new Point(x, y));
            }
            else
            {
                var fmt = new Avalonia.Media.FormattedText(txt, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, FontSz, Brushes.Black);
                ctx.DrawText(fmt, new Point(x, y));
            }

            rowIdx++;
        }
    }

    protected override async void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        int row = (int)Math.Floor(pos.Y / RowH);

        int count = -1;
        for (int i = 0; i < _items.Count; i++)
        {
            if (!Hidden[i])
            {
                count++;
                if (count == row)
                {
                    await _owner.SetSelectedIndexAsync(i);
                    break;
                }
            }
        }
    }
}
