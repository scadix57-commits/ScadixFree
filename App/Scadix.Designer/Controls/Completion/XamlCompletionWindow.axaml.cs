using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Scadix.Designer.Controls.Completion;

/// <summary>
/// نافذة إكمال موحدة — مطابقة تامة لـ CSharpEditor.CompletionWindow
/// 13 زر فلتر بنفس الترتيب والمنطق
/// </summary>
public partial class XamlCompletionWindow : UserControl
{
    // ── الحالة ────────────────────────────────────────────────────────────
    public int MaxWindowHeight { get; set; } = 213;
    public bool AnchorOnTop { get; set; }
    public double TopAnchor { get; set; }
    public int VisibleItems { get; private set; }
    public int TotalItems { get; private set; }
    public string? FilterText { get; private set; }

    public int SelectedIndex => _list.SelectedIndex;

    public int EffectiveSelectedIndex
    {
        get
        {
            if (_list.SelectedIndex >= 0 &&
                _list.SelectedIndex < _list.Items.Count &&
                !_list.Hidden[_list.SelectedIndex])
            {
                int prevItems = 0;
                for (int i = 0; i < Math.Min(_list.SelectedIndex + 1, TotalItems); i++)
                    if (!_list.Hidden[i]) prevItems++;
                return prevItems - 1;
            }
            return -1;
        }
    }

    // ── الأحداث ───────────────────────────────────────────────────────────
    public event EventHandler<XamlCompletionItem>? Committed;

    // ── FilterKinds: مطابق تماماً لـ CSharpEditor (13 زر بنفس الترتيب) ──
    // Index:  0=Namespace, 1=Class, 2=Struct, 3=Interface, 4=Enum, 5=Delegate,
    //         6=Field,     7=Event, 8=Property, 9=Method, 10=Local, 11=Keyword, 12=Unknown
    private static readonly XamlCompletionKind[] FilterKinds =
    {
        XamlCompletionKind.Namespace,        // 0:  NamespaceIcon
        XamlCompletionKind.Element,          // 1:  ClassIcon
        XamlCompletionKind.Struct,           // 2:  StructIcon
        XamlCompletionKind.Interface,        // 3:  InterfaceIcon
        XamlCompletionKind.Enum,             // 4:  EnumIcon
        XamlCompletionKind.Delegate,         // 5:  DelegateIcon
        XamlCompletionKind.AttachedProperty, // 6:  FieldIcon  (Field/EnumMember)
        XamlCompletionKind.Event,            // 7:  EventIcon
        XamlCompletionKind.Property,         // 8:  PropertyIcon
        XamlCompletionKind.MarkupExtension,  // 9:  MethodIcon
        XamlCompletionKind.BindingPath,      // 10: LocalIcon
        XamlCompletionKind.Snippet,          // 11: KeywordIcon
        XamlCompletionKind.Value,            // 12: UnknownIcon
    };

    // ── عناصر التحكم الداخلية ────────────────────────────────────────────
    private XamlCompletionListControl _list = null!;
    private ScrollViewer _scroll = null!;
    private Border _docBorder = null!;
    private TextBlock _docText = null!;
    private readonly List<ToggleButton> _filterBtns = new();
    private bool _updatingItems;

    // ── البيانات ──────────────────────────────────────────────────────────
    private ImmutableList<XamlCompletionItem> _allItems = ImmutableList<XamlCompletionItem>.Empty;

    // ─────────────────────────────────────────────────────────────────────
    public XamlCompletionWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        Focusable = false;
        _scroll    = this.FindControl<ScrollViewer>("ItemsScrollViewer")!;
        _docBorder = this.FindControl<Border>("DocBorder")!;
        _docText   = this.FindControl<TextBlock>("DocText")!;

        _list = new XamlCompletionListControl(this);
        _scroll.Content = _list;

        // جمع أزرار الفلاتر الـ 13 — مطابق لـ CSharpEditor
        var container = this.FindControl<StackPanel>("FilterContainer")!;
        foreach (Control ctrl in container.Children)
        {
            if (ctrl is ToggleButton tb)
            {
                _filterBtns.Add(tb);
                // مطابق لـ CSharpEditor: PropertyChanged على IsCheckedProperty
                tb.IsCheckedChanged += async (_, _) =>
                {
                    if (!_updatingItems) await UpdateFilter();
                };
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // API العام — مطابق لـ CSharpEditor
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// تحميل قائمة جديدة — مطابق لـ CSharpEditor.SetCompletionList
    /// </summary>
    public async Task SetItemsAsync(IEnumerable<XamlCompletionItem> items)
    {
        _updatingItems = true;

        // حفظ العنصر المحدد السابق (مثل CSharpEditor)
        XamlCompletionItem? previousSelectedItem = null;
        if (SelectedIndex >= 0 && SelectedIndex < _list.Items.Count)
            previousSelectedItem = _list.Items[SelectedIndex];

        // حفظ الـ offset السابق (مثل CSharpEditor)
        Vector? previousOffset = IsVisible ? (Vector?)_scroll.Offset : null;

        await SetSelectedIndexAsync(-1);

        _allItems  = ImmutableList.CreateRange(items);
        TotalItems = _allItems.Count;

        // تحديد أي فلاتر مرئية — مطابق لـ CSharpEditor
        bool[] filters = new bool[_filterBtns.Count];
        int newSelectedIndex = -1;
        int index = 0;

        foreach (var item in _allItems)
        {
            int fi = GetFilterIndex(item.Kind);
            if (fi >= 0 && fi < filters.Length)
                filters[fi] = true;

            if (previousSelectedItem != null && newSelectedIndex < 0 &&
                item.Text == previousSelectedItem.Text && item.Kind == previousSelectedItem.Kind)
                newSelectedIndex = index;

            index++;
        }

        _list.Items = _allItems;

        // تحديث رؤية الأزرار — مطابق لـ CSharpEditor
        for (int i = 0; i < _filterBtns.Count; i++)
        {
            _filterBtns[i].IsVisible = i < filters.Length && filters[i];
            if (!IsVisible) _filterBtns[i].IsChecked = false;
        }

        // حساب العرض — مطابق لـ CSharpEditor
        Width = Math.Max(
            filters.Count(a => a) * 30 + 2,
            _list.MaxTextWidth + 2 + 2 + 20 + 25);

        // تعديل X إذا تجاوز حدود الـ parent (مثل CSharpEditor)
        if (RenderTransform is TranslateTransform rt && Parent is Control parentCtrl)
        {
            RenderTransform = new TranslateTransform(
                Math.Min(parentCtrl.Bounds.Width - Width, rt.X), rt.Y);
        }

        _updatingItems = false;

        await UpdateFilter();

        // استعادة الـ offset السابق (مثل CSharpEditor)
        if (previousOffset != null)
            _scroll.Offset = previousOffset.Value;

        if (newSelectedIndex >= 0 || (SelectedIndex < 0 && TotalItems > 0))
            await SetSelectedIndexAsync(Math.Max(0, newSelectedIndex));

        _list.InvalidateMeasure();
        _list.InvalidateVisual();
    }

    /// <summary>تعيين نص الفلتر — مطابق لـ CSharpEditor.SetFilterText</summary>
    public async Task SetFilterTextAsync(string? text)
    {
        FilterText   = text;
        _list.Filter = text;
        await UpdateFilter();
    }

    /// <summary>تحديد العنصر — مطابق لـ CSharpEditor.SetSelectedIndex</summary>
    public async Task SetSelectedIndexAsync(int index)
    {
        if (index >= _list.Items.Count) return;

        _list.SelectedIndex = index;
        _list.InvalidateVisual();

        if (!_updatingItems && _list.SelectedIndex >= 0)
        {
            if (_list.SelectedIndex < _list.Hidden.Length &&
                !_list.Hidden[_list.SelectedIndex])
            {
                ShowDocumentation(_list.SelectedIndex);
                await UpdateScrollViewer();
            }
            else
            {
                _docBorder.IsVisible = false;
                await UpdateScrollViewer();
            }
        }
    }

    /// <summary>تحريك التحديد — مطابق لـ CSharpEditor.UpdateSelectedIndex</summary>
    public async Task MoveSelectionAsync(int delta)
    {
        int cur = Math.Max(_list.SelectedIndex, 0);
        int idx = cur;

        if (delta > 0)
        {
            for (int i = cur + 1; i < _list.Items.Count && delta > 0; i++)
                if (!_list.Hidden[i]) { idx = i; delta--; }
        }
        else
        {
            for (int i = cur - 1; i >= 0 && delta < 0; i--)
                if (!_list.Hidden[i]) { idx = i; delta++; }
        }

        await SetSelectedIndexAsync(idx);
    }

    /// <summary>تأكيد العنصر — مطابق لـ CSharpEditor.Commit</summary>
    public void Commit()
    {
        if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _list.Items.Count) return;
        var item = _list.Items[_list.SelectedIndex];
        Committed?.Invoke(this, item);
        IsVisible = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // UpdateFilter — مطابق تماماً لـ CSharpEditor.UpdateFilter
    // ─────────────────────────────────────────────────────────────────────

    private async Task UpdateFilter()
    {
        // أي فلاتر مفعّلة؟
        bool[] filters = new bool[_filterBtns.Count];
        for (int i = 0; i < _filterBtns.Count; i++)
            filters[i] = _filterBtns[i].IsVisible && _filterBtns[i].IsChecked == true;

        // مثل CSharpEditor: إذا لا يوجد فلتر مفعّل → كل شيء مرئي
        if (!filters.Any(a => a))
            for (int i = 0; i < filters.Length; i++)
                filters[i] = true;

        bool[] foundFilters           = new bool[_filterBtns.Count];
        bool[] foundButFilteredFilters = new bool[_filterBtns.Count];

        string? filter = FilterText?.Trim();

        int firstVisibleIndex = -1;
        int visibleCount      = 0;
        int firstExactMatch   = -1;

        for (int i = 0; i < _list.Items.Count; i++)
        {
            var item   = _list.Items[i];
            int fi     = GetFilterIndex(item.Kind);
            bool kindOk = fi < 0 || (fi < filters.Length && filters[fi]);

            _list.Hidden[i] = !kindOk;

            // تسجيل وجود العنصر في الفلتر
            if (fi >= 0 && fi < foundButFilteredFilters.Length)
            {
                if (string.IsNullOrEmpty(filter) ||
                    item.Text.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                    foundButFilteredFilters[fi] = true;
            }

            // تطبيق فلتر النص
            if (!string.IsNullOrEmpty(filter) &&
                !item.Text.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                _list.Hidden[i] = true;

            if (!_list.Hidden[i])
            {
                if (fi >= 0 && fi < foundFilters.Length)
                    foundFilters[fi] = true;

                if (firstVisibleIndex < 0) firstVisibleIndex = i;
                visibleCount++;

                if (!string.IsNullOrEmpty(filter) && firstExactMatch < 0 &&
                    item.Text.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                    firstExactMatch = i;
            }
        }

        // تحديث حالة أزرار الفلتر — مطابق لـ CSharpEditor
        for (int i = 0; i < _filterBtns.Count; i++)
        {
            _filterBtns[i].IsEnabled = foundFilters[i] || foundButFilteredFilters[i];
            _filterBtns[i].Opacity   = _filterBtns[i].IsEnabled ? 1.0 : 0.25;
        }

        // تحديث التحديد — مطابق لـ CSharpEditor
        if ((SelectedIndex < 0 ||
             (filter != null && SelectedIndex < _list.Items.Count &&
              !_list.Items[SelectedIndex].Text.StartsWith(filter, StringComparison.OrdinalIgnoreCase))) &&
            firstExactMatch >= 0)
        {
            await SetSelectedIndexAsync(firstExactMatch);
        }
        else if ((SelectedIndex < 0 ||
                  (SelectedIndex < _list.Hidden.Length && _list.Hidden[SelectedIndex])) &&
                 firstVisibleIndex >= 0)
        {
            await SetSelectedIndexAsync(firstVisibleIndex);
        }

        // تحديث حجم النافذة — مطابق لـ CSharpEditor
        if (!AnchorOnTop)
        {
            Height = Math.Min(MaxWindowHeight, visibleCount * 20 + 33);
        }
        else
        {
            Height = Math.Min(MaxWindowHeight, visibleCount * 20 + 33);
            if (RenderTransform is TranslateTransform tt)
                RenderTransform = new TranslateTransform(tt.X, TopAnchor - Height);
        }

        VisibleItems = visibleCount;

        await UpdateScrollViewer();

        if (visibleCount == 0)
            IsVisible = false;

        _list.InvalidateMeasure();

        // مطابق لـ CSharpEditor: SetSelectedIndex مرة أخرى بعد كل شيء
        await SetSelectedIndexAsync(SelectedIndex);
    }

    // ─────────────────────────────────────────────────────────────────────
    // مساعد: تحويل Kind → filter index
    // ─────────────────────────────────────────────────────────────────────

    private static int GetFilterIndex(XamlCompletionKind kind)
    {
        for (int i = 0; i < FilterKinds.Length; i++)
            if (FilterKinds[i] == kind) return i;
        return 12; // Unknown
    }

    // ─────────────────────────────────────────────────────────────────────
    // التمرير — مطابق لـ CSharpEditor.UpdateScrollViewer
    // ─────────────────────────────────────────────────────────────────────

    private async Task UpdateScrollViewer()
    {
        int eff = EffectiveSelectedIndex;
        if (eff >= 0)
        {
            var newOffset = new Vector(
                _scroll.Offset.X,
                Math.Max(
                    Math.Min(eff * 20, _scroll.Offset.Y),
                    eff * 20 + 20 - (Height - 33)));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _scroll.Offset = newOffset;
            }, DispatcherPriority.Input);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // التوثيق الجانبي
    // ─────────────────────────────────────────────────────────────────────

    private void ShowDocumentation(int index)
    {
        if (index < 0 || index >= _list.Items.Count ||
            index >= _list.Hidden.Length || _list.Hidden[index])
        {
            _docBorder.IsVisible = false;
            return;
        }

        var item = _list.Items[index];
        string doc = item.Description;
        if (!string.IsNullOrEmpty(item.Detail))
            doc += $"\nType: {item.Detail}";

        if (string.IsNullOrWhiteSpace(doc))
        {
            _docBorder.IsVisible = false;
            return;
        }

        _docText.Text = doc;

        double docW = Math.Min(300, Math.Max(150, doc.Length * 6.0));
        double docH = Math.Min(150, Math.Max(40, (doc.Length / 40 + 1) * 20.0));

        _docBorder.Width  = docW;
        _docBorder.Height = docH;

        double offset = EffectiveSelectedIndex * 20.0 - _scroll.Offset.Y;
        _docBorder.RenderTransform = new TranslateTransform(Width + 4, Math.Max(0, offset));
        _docBorder.IsVisible = true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // تحديد موضع النافذة — مطابق لـ CSharpEditor.OpenCompletion
    // ─────────────────────────────────────────────────────────────────────

    public void PositionAt(Rect caretRect, Control parent)
    {
        double parentH    = parent.Bounds.Height;
        double parentW    = parent.Bounds.Width;
        double x          = caretRect.X;
        double yBelow     = caretRect.Bottom + 2;
        double availBelow = parentH - yBelow;
        double availAbove = caretRect.Top;
        double reqH       = Math.Min(MaxWindowHeight, VisibleItems * 20 + 33);

        if (reqH <= availBelow || availBelow >= availAbove)
        {
            MaxWindowHeight = 213;
            AnchorOnTop     = false;
            Height          = Math.Min(reqH, availBelow);
            RenderTransform = new TranslateTransform(
                Math.Min(x, parentW - Width - 4), yBelow);
        }
        else
        {
            MaxWindowHeight = (int)Math.Round(availAbove);
            AnchorOnTop     = true;
            TopAnchor       = caretRect.Top - 2;
            Height          = Math.Min(reqH, availAbove);
            RenderTransform = new TranslateTransform(
                Math.Min(x, parentW - Width - 4), TopAnchor - Height);
        }
    }
}
