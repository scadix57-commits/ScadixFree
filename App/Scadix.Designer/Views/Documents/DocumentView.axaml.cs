using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Scadix.AxamlDesigner;
using Scadix.AxamlDesigner.Services;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Xaml;
using Scadix.AxamlDom;
using Scadix.Designer;
using Scadix.Designer.Services;
using Scadix.Designer.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;

namespace Scadix.Designer;

public partial class DocumentView : UserControl, ISplitResizeOverlayService, ISplitModeService
{
    public Document? Document { get; private set; }
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private GridLength _editorWidth = new(1, GridUnitType.Star);
    private GridLength _previewWidth = new(1, GridUnitType.Star);
    private bool _wasSplit;
    private bool _subscribed;
    private ISelectionService? _selection;
    private SplitGroupCommandService? _groupCommands;
    private bool _syncingSelection;
    private readonly List<(int Start, int End, DesignItem Item)> _sourceControls = new();
    private (int[] Items, int Primary, string Source)? _pendingGroupSelection;
    private (int[] Starts, string Source)? _pendingClipboardSelection;
private string? _splitClipboardText;
    private string[]? _splitClipboardPasteFragments;
    private int? _splitClipboardParentStart;

    // Inline text editing
    private Canvas? _inlineEditOverlay;
    private TextBox? _inlineEditTextBox;
    private DesignItem? _inlineEditItem;
    private DesignItemProperty? _inlineEditProperty;
    private DispatcherTimer? _inlineEditCommitTimer;

    // Quick actions panel
    private Border? _quickActionsPanel;
    private StackPanel? _quickActionsContent;
    private readonly string[] _quickActionPropertyNames = new[]
    {
        "HorizontalAlignment", "VerticalAlignment", "Margin", "Width", "Height",
        "Background", "Foreground", "BorderBrush", "BorderThickness"
    };

    // Marquee selection
    private Border? _marqueeRect;
    private Point _marqueeStart;
    private bool _isMarqueePending;
    private bool _isMarqueeing;
    private IPointer? _marqueePointer;
    private DesignItem[] _marqueeInitialSelection = Array.Empty<DesignItem>();
    private DesignItem? _marqueeInitialPrimary;

    // Snap settings
    public bool SnapEnabled { get; set; } = true;
    public double SnapGridSize { get; set; } = 8;

    // Alignment guides settings
    public double AlignmentGuideThreshold { get; set; } = 4;
    public GuideExtent AlignmentGuideExtent { get; set; } = GuideExtent.BetweenControls;

    // ISplitResizeOverlayService implementation
    public Canvas? OverlayCanvas => SplitResizeOverlay;
    public Control? DesignRoot => Document?.DesignContext?.RootItem?.View as Control;

    // ISplitModeService implementation
    public bool IsSplitMode => Document?.IsSplitMode == true;

    public Func<double?, double?, double?, double?, bool>? CreateResizeCommit(DesignItem item)
        => Document?.IsPreviewSelectable == true
            ? new SplitPropertyEditorFactory(Document).CreateResizeCommit(item) : null;

    public Func<double, double, bool>? CreateMoveCommit(DesignItem item)
        => Document?.IsPreviewSelectable == true
            ? new SplitPropertyEditorFactory(Document).CreateMoveCommit(item) : null;

    public Func<IReadOnlyList<Rect>, bool>? CreateGroupCommit(IReadOnlyList<DesignItem> items, bool includeSize)
        => Document?.IsPreviewSelectable == true
            ? new SplitPropertyEditorFactory(Document).CreateGroupCommit(items, includeSize) : null;

public void RefreshAfterKeyboardEdit()
    {
        _previewTimer.Stop();
        Document?.RefreshPreview();
        Document?.DesignSurface.UpdateLayout();
    }

    public void ShowSnapReadout(Point position, Size? size = null, Point? overlayPosition = null)
    {
        if (SnapReadout == null || SnapReadoutText == null) return;
        var text = size.HasValue
            ? $"W: {size.Value.Width:0}  H: {size.Value.Height:0}"
            : $"X: {position.X:0}  Y: {position.Y:0}";
        SnapReadoutText.Text = text;
        var anchor = overlayPosition ?? position;
        Canvas.SetLeft(SnapReadout, anchor.X + 12);
        Canvas.SetTop(SnapReadout, anchor.Y + 12);
        SnapReadout.IsVisible = true;
    }

    public void HideSnapReadout()
    {
        if (SnapReadout != null) SnapReadout.IsVisible = false;
    }

    public void ShowAlignmentGuides(IEnumerable<LineGuide> guides)
    {
        if (AlignmentGuidesOverlay == null) return;
        AlignmentGuidesOverlay.Children.Clear();

        IBrush brush = this.FindResource("SystemAccentColor") is Color accentColor
            ? new SolidColorBrush(accentColor)
            : Brushes.DodgerBlue;

        // High contrast fallback
        if (Application.Current?.ActualThemeVariant == ThemeVariant.Dark)
        {
            brush = this.FindResource("SystemAltHighColor") is Color highColor
                ? new SolidColorBrush(highColor)
                : Brushes.Yellow;
        }

        foreach (var guide in guides)
        {
            var line = new Line
            {
                Stroke = brush,
                StrokeThickness = 1,
                StrokeDashArray = new AvaloniaList<double> { 4, 2 },
                IsHitTestVisible = false
            };

            if (guide.Orientation == Orientation.Vertical)
            {
                line.StartPoint = new Point(guide.Position, guide.Start);
                line.EndPoint = new Point(guide.Position, guide.End);
            }
            else
            {
                line.StartPoint = new Point(guide.Start, guide.Position);
                line.EndPoint = new Point(guide.End, guide.Position);
            }

            AlignmentGuidesOverlay.Children.Add(line);
        }
    }

    public void HideAlignmentGuides()
    {
        if (AlignmentGuidesOverlay != null) AlignmentGuidesOverlay.Children.Clear();
    }

public DocumentView()
    {
        InitializeComponent();
        SplitResizeOverlay.PointerPressed += (_, e) =>
        {
            if (!e.Handled && e.KeyModifiers.HasFlag(KeyModifiers.Control)) PreviewPointerPressed(this, e);
        };

        // Marquee selection handlers on the preview and interaction overlays.
        PreviewSelectionOverlay.PointerMoved += OnMarqueePointerMoved;
        PreviewSelectionOverlay.PointerReleased += OnMarqueePointerReleased;
        PreviewSelectionOverlay.PointerCaptureLost += OnMarqueePointerCaptureLost;
        PreviewSelectionOverlay.DoubleTapped += OnPreviewDoubleTapped;
        SplitResizeOverlay.KeyDown += OnMarqueeKeyDown;

        // Quick actions panel - find the named controls
        _quickActionsPanel = this.FindControl<Border>("QuickActionsPanel");
        _quickActionsContent = this.FindControl<StackPanel>("QuickActionsContent");
        _inlineEditOverlay = this.FindControl<Canvas>("InlineEditOverlay");

        this.Loaded += DocumentView_Loaded;
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); Document?.RefreshPreview(); };
        AttachedToVisualTree += (_, _) => Subscribe();
        DetachedFromVisualTree += (_, _) =>
        {
            _previewTimer.Stop();
            if (Document != null)
            {
                Document.ApplySourceEdit = null;
                Document.ChangeSourceHistory = null;
            }
            if (Document != null && _subscribed) Document.PropertyChanged -= DocumentChanged;
            _subscribed = false;
            if (uxXamlEditor.Editor != null)
            {
                uxXamlEditor.Editor.TextArea.Caret.PositionChanged -= SourceCaretChanged;
                uxXamlEditor.Editor.TextArea.SelectionChanged -= SourceCaretChanged;
            }
            SubscribeSelection(null);
        };
    }

    private void DocumentView_Loaded(object? sender, RoutedEventArgs e)
    {
        this.Loaded -= DocumentView_Loaded;

        Document = (Document)this.DataContext!;
        MainWindowViewModel.Instance.Views[Document] = this;

        uxXamlEditor.AttachDocument(Document);

        // Non-XAML files â†’ editor only; XAML/AXAML â†’ Design mode
        Document.Mode = Document.IsXamlFile
            ? DocumentMode.Design
            : DocumentMode.Xaml;

        Subscribe();
        UpdateLayoutMode();
    }

    private void Subscribe()
    {
        if (Document == null || _subscribed) return;
        uxXamlEditor.AttachDocument(Document);
        Document.PropertyChanged += DocumentChanged;
        _subscribed = true;
        Document.ApplySourceEdit = ApplySourceEdit;
        Document.ChangeSourceHistory = redo =>
        {
            if (uxXamlEditor.Editor is not { } editor) return;
            if (redo) editor.Document.UndoStack.Redo();
            else editor.Document.UndoStack.Undo();
        };
        if (uxXamlEditor.Editor != null)
        {
            uxXamlEditor.Editor.TextArea.Caret.PositionChanged += SourceCaretChanged;
            uxXamlEditor.Editor.TextArea.SelectionChanged += SourceCaretChanged;
        }
        SubscribeSelection(Document.SelectionService);
        if (Document.IsSplitMode) { _previewTimer.Stop(); _previewTimer.Start(); }
    }

    private void DocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Document.SelectionService))
            SubscribeSelection(Document?.SelectionService);
        if (e.PropertyName == nameof(Document.Mode))
        {
            _previewTimer.Stop();
            EndMarquee();
            UpdateLayoutMode();
        }
        else if (e.PropertyName == nameof(Document.Text) && Document?.IsSplitMode == true)
        {
            _previewTimer.Stop();
            EndMarquee();
            _previewTimer.Start();
        }
    }

    private void PreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OnMarqueePointerPressed(sender, e);
        if (e.Handled) return;

        e.Handled = true;
        if (Document?.IsPreviewSelectable != true || !e.GetCurrentPoint(PreviewSelectionOverlay).Properties.IsLeftButtonPressed)
            return;
        var context = Document.DesignContext;
        var root = context.RootItem?.View;
        DesignItem? selected = null;
        if (root != null)
        {
            foreach (var hit in root.GetVisualsAt(e.GetPosition(root)))
            {
                for (Visual? visual = hit; visual != null; visual = visual.GetVisualParent())
                {
                    selected = context.Services.View.GetModel(visual);
                    if (selected != null || visual == root) break;
                }
                if (selected != null) break;
            }
        }
        var alreadySelected = selected != null && ReferenceEquals(Document.SelectionService!.PrimarySelection, selected);
        var canToggle = selected?.Component is Control
            && !ReferenceEquals(selected, context.RootItem)
            && selected.Parent?.Component is Canvas or Grid
            && Document.SelectionService!.SelectionCount > 0
            && Document.SelectionService.SelectedItems.All(item => item.Component is Control
                && ReferenceEquals(item.Parent, selected.Parent));
        var selectionType = e.KeyModifiers.HasFlag(KeyModifiers.Control) && canToggle
            ? SelectionTypes.Toggle
            : SelectionTypes.Replace;
        Document.SelectionService!.SetSelectedComponents(selected == null
            ? Array.Empty<DesignItem>() : new[] { selected }, selectionType);
        if (alreadySelected) NavigateToPreviewSelection();
        if (selected != null) SplitResizeOverlay.Focus();
    }

    private void OnMarqueePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Document?.IsPreviewSelectable != true
            || !e.GetCurrentPoint(PreviewSelectionOverlay).Properties.IsLeftButtonPressed
            || _isMarqueeing)
            return;

        var context = Document.DesignContext;
        var root = context.RootItem?.View;
        if (root == null) return;

        // Check if click is on empty space (no control hit)
        var hitPosition = e.GetPosition(root);
        var hitControl = false;
        foreach (var hit in root.GetVisualsAt(hitPosition))
        {
            for (Visual? visual = hit; visual != null; visual = visual.GetVisualParent())
            {
                var model = context.Services.View.GetModel(visual);
                if (model != null
                    && model.Component is Control and not (Canvas or Grid)
                    && !ReferenceEquals(model, context.RootItem))
                {
                    hitControl = true;
                    break;
                }
                if (visual == root) break;
            }
            if (hitControl) break;
        }

        if (hitControl) return; // Let normal click handling take over

        _isMarqueePending = true;
        _marqueePointer = e.Pointer;
        _marqueeStart = e.GetPosition(PreviewSelectionOverlay);
        _marqueeInitialSelection = Document.SelectionService?.SelectedItems.ToArray()
            ?? Array.Empty<DesignItem>();
        _marqueeInitialPrimary = Document.SelectionService?.PrimarySelection;
    }

    private void OnMarqueePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isMarqueePending && !_isMarqueeing && ReferenceEquals(e.Pointer, _marqueePointer))
        {
            var position = e.GetPosition(PreviewSelectionOverlay);
            if (Math.Abs(position.X - _marqueeStart.X) < 3
                && Math.Abs(position.Y - _marqueeStart.Y) < 3)
                return;

            if (Document?.SelectionService is { } selection)
            {
                selection.SetSelectedComponents(_marqueeInitialSelection, SelectionTypes.Replace);
                if (_marqueeInitialPrimary != null)
                    selection.SetSelectedComponents(new[] { _marqueeInitialPrimary },
                        SelectionTypes.Primary | SelectionTypes.Add);
            }

            _isMarqueeing = true;
            _marqueeRect = new Border
            {
                Name = "SplitMarqueeSelection",
                BorderBrush = Brushes.DodgerBlue,
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(30, 30, 144, 255)),
                IsHitTestVisible = false
            };
            SplitResizeOverlay.Children.Add(_marqueeRect);
            e.Pointer.Capture(PreviewSelectionOverlay);
            SplitResizeOverlay.Focus();
        }

        if (!_isMarqueeing || _marqueeRect == null) return;

        var current = e.GetPosition(PreviewSelectionOverlay);
        var x = Math.Min(_marqueeStart.X, current.X);
        var y = Math.Min(_marqueeStart.Y, current.Y);
        var width = Math.Abs(current.X - _marqueeStart.X);
        var height = Math.Abs(current.Y - _marqueeStart.Y);

        Canvas.SetLeft(_marqueeRect, x);
        Canvas.SetTop(_marqueeRect, y);
        _marqueeRect.Width = width;
        _marqueeRect.Height = height;

        e.Handled = true;
    }

    private void OnMarqueePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isMarqueeing || _marqueeRect == null)
        {
            if (_isMarqueePending) EndMarquee();
            return;
        }

        var context = Document?.DesignContext;
        var root = context?.RootItem?.View;
        var selection = Document?.SelectionService;

        if (context != null && root != null && selection != null)
        {
            // Convert marquee rect to design surface coordinates
            var marqueeRect = new Rect(
                Canvas.GetLeft(_marqueeRect),
                Canvas.GetTop(_marqueeRect),
                _marqueeRect.Width,
                _marqueeRect.Height);

            // Transform to root coordinates
            var transform = PreviewSelectionOverlay.TransformToVisual(root);
            if (transform.HasValue)
            {
                var rootRect = marqueeRect.TransformToAABB(transform.Value);

                var allControls = new List<(DesignItem Item, Rect Bounds)>();
                CollectSelectableControls(context.RootItem, allControls);

                var intersecting = allControls
                    .Where(c => c.Bounds.Intersects(rootRect) && c.Item.Parent?.Component is Canvas or Grid)
                    .Select(c => c.Item)
                    .ToArray();

                if (intersecting.Length > 0)
                {
                    var parent = intersecting[0].Parent;
                    var sameParent = intersecting
                        .Where(item => ReferenceEquals(item.Parent, parent))
                        .ToArray();
                    var canToggle = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                        && (selection.SelectionCount == 0
                            || selection.SelectedItems.All(item => ReferenceEquals(item.Parent, parent)));
                    selection.SetSelectedComponents(sameParent,
                        canToggle ? SelectionTypes.Toggle : SelectionTypes.Replace);
                }
                else if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    selection.SetSelectedComponents(Array.Empty<DesignItem>(), SelectionTypes.Replace);
            }
        }

        EndMarquee();
        e.Handled = true;
    }

    private void OnMarqueePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isMarqueeing) EndMarquee();
    }

    private async void OnMarqueeKeyDown(object? sender, KeyEventArgs e)
    {
        if (_isMarqueeing && e.Key == Key.Escape)
        {
            EndMarquee();
            e.Handled = true;
            return;
        }

        if (Document?.IsPreviewSelectable != true || Document.DesignSurface == null)
            return;

        var designSurface = Document.DesignSurface;
        var selection = Document.SelectionService;
        var keyModifiers = e.KeyModifiers;

        if (keyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.C when designSurface.CanCopy():
                    e.Handled = await CopySelectionAsync(selection);
                    break;
                case Key.X when designSurface.CanCut():
                    if (await CopySelectionAsync(selection) && DeleteSelection(selection))
                    {
                        RefreshAfterKeyboardEdit();
                        e.Handled = true;
                    }
                    break;
                case Key.V:
                    if (await PasteSelectionAsync(selection))
                    {
                        RefreshAfterKeyboardEdit();
                        e.Handled = true;
                    }
                    break;
                case Key.D when designSurface.CanCopy():
                    if (DuplicateSelection(selection))
                    {
                        RefreshAfterKeyboardEdit();
                        e.Handled = true;
                    }
                    break;
            }
        }
        else if (e.Key == Key.Delete && designSurface.CanDelete())
        {
            if (DeleteSelection(selection))
            {
                RefreshAfterKeyboardEdit();
                e.Handled = true;
            }
        }
    }

    private bool DuplicateSelection(ISelectionService? selection)
    {
        var sourceSelection = GetSourceSelection(selection);
        if (sourceSelection == null) return false;
        var entries = sourceSelection.Value.Entries;
        var insertion = entries.Max(entry => entry.End);
        var separator = SourceSeparator(entries[0].Start);
        var fragments = entries.Select(entry => OffsetFragment(
            uxXamlEditor.Editor!.Text.Substring(entry.Start, entry.End - entry.Start), entry.Item)).ToArray();
        var inserted = separator + string.Join(separator, fragments);
        var starts = new int[fragments.Length];
        var cursor = insertion + separator.Length;
        for (var index = 0; index < fragments.Length; index++)
        {
            starts[index] = cursor;
            cursor += fragments[index].Length + (index + 1 < fragments.Length ? separator.Length : 0);
        }
        return ApplyClipboardSourceEdit(insertion, 0, inserted, starts);
    }

    private bool DeleteSelection(ISelectionService? selection)
    {
        var sourceSelection = GetSourceSelection(selection);
        if (sourceSelection == null) return false;
        var entries = sourceSelection.Value.Entries;
        var start = entries.Min(entry => entry.Start);
        var end = entries.Max(entry => entry.End);
        var replacement = uxXamlEditor.Editor!.Text.Substring(start, end - start);
        foreach (var entry in entries.OrderByDescending(entry => entry.Start))
            replacement = replacement.Remove(entry.Start - start, entry.End - entry.Start);
        return ApplyClipboardSourceEdit(start, end - start, replacement, null);
    }

    private async Task<bool> CopySelectionAsync(ISelectionService? selection)
    {
        var sourceSelection = GetSourceSelection(selection);
        if (sourceSelection == null || uxXamlEditor.Editor == null) return false;
        const char delimiter = (char)0x7F;
        var entries = sourceSelection.Value.Entries;
        var fragments = entries.Select(entry => uxXamlEditor.Editor.Text
            .Substring(entry.Start, entry.End - entry.Start)).ToArray();
        _splitClipboardText = string.Join(delimiter, fragments) + delimiter;
        _splitClipboardPasteFragments = entries
            .Select((entry, index) => OffsetFragment(fragments[index], entry.Item)).ToArray();
        _splitClipboardParentStart = _sourceControls
            .FirstOrDefault(entry => ReferenceEquals(entry.Item, sourceSelection.Value.Parent)).Start;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.ClearAsync();
            await clipboard.SetTextAsync(_splitClipboardText);
        }
        return true;
    }

    private async Task<bool> PasteSelectionAsync(ISelectionService? selection)
    {
        if (uxXamlEditor.Editor == null) return false;
        const char delimiter = (char)0x7F;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        var text = clipboard == null ? _splitClipboardText : await clipboard.GetTextAsync();
        if (string.IsNullOrEmpty(text)) return false;
        var isInternalClipboard = text == _splitClipboardText && _splitClipboardPasteFragments != null;
        var fragments = isInternalClipboard
            ? _splitClipboardPasteFragments
            : text.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
        if (fragments.Length == 0) return false;
        if (!isInternalClipboard)
        {
            if (Document?.DesignContext is not XamlDesignContext xamlContext
                || xamlContext.RootItem is not XamlDesignItem rootItem) return false;
            try
            {
                if (fragments.Any(fragment => XamlParser.ParseSnippet(
                        rootItem.XamlObject, fragment, xamlContext.ParserSettings) == null)) return false;
            }
            catch
            {
                return false;
            }
        }

        DesignItem? parent = null;
        var current = GetSourceSelection(selection);
        if (current != null) parent = current.Value.Parent;
        if (parent == null && _splitClipboardParentStart is { } parentStart)
            parent = _sourceControls.FirstOrDefault(entry => entry.Start == parentStart).Item;
        if (parent?.Component is not (Canvas or Grid)) return false;
        var parentEntry = _sourceControls.FirstOrDefault(entry => ReferenceEquals(entry.Item, parent));
        if (parentEntry.Item == null) return false;

        var insertion = current != null
            ? current.Value.Entries.Max(entry => entry.End)
            : FindClosingTagStart(uxXamlEditor.Editor.Text, parentEntry.Start, parentEntry.End);
        if (insertion < 0) return false;
        var separator = SourceSeparator(current?.Entries[0].Start ?? insertion);
        var inserted = separator + string.Join(separator, fragments);
        var starts = new int[fragments.Length];
        var cursor = insertion + separator.Length;
        for (var index = 0; index < fragments.Length; index++)
        {
            starts[index] = cursor;
            cursor += fragments[index].Length + (index + 1 < fragments.Length ? separator.Length : 0);
        }
        return ApplyClipboardSourceEdit(insertion, 0, inserted, starts);
    }

    private static int FindClosingTagStart(string source, int start, int end)
    {
        var index = source.LastIndexOf("</", Math.Min(end - 1, source.Length - 1), StringComparison.Ordinal);
        return index >= start ? index : -1;
    }

    private (DesignItem Parent, (int Start, int End, DesignItem Item)[] Entries)? GetSourceSelection(ISelectionService? selection)
    {
        if (selection == null || selection.SelectionCount == 0 || uxXamlEditor.Editor == null) return null;
        var selected = selection.SelectedItems.ToArray();
        var parent = selected[0].Parent;
        if (parent?.Component is not (Canvas or Grid)
            || selected.Any(item => !ReferenceEquals(item.Parent, parent))) return null;
        var entries = selected.Select(item => _sourceControls.SingleOrDefault(entry => ReferenceEquals(entry.Item, item))).ToArray();
        if (entries.Any(entry => entry.Item == null)) return null;
        return (parent, entries.OrderBy(entry => entry.Start).ToArray());
    }

    private bool ApplyClipboardSourceEdit(int start, int length, string replacement, int[]? selectedStarts)
    {
        if (Document?.IsPreviewSelectable != true || uxXamlEditor.Editor is not { } editor) return false;
        var source = editor.Text.Remove(start, length).Insert(start, replacement);
        _pendingGroupSelection = null;
        _pendingClipboardSelection = selectedStarts == null ? null : (selectedStarts, source);
        _syncingSelection = true;
        try
        {
            using (editor.Document.RunUpdate())
                editor.Document.Replace(start, length, replacement);
            editor.Select(start, 0);
        }
        finally { _syncingSelection = false; }
        return true;
    }

    private string SourceSeparator(int elementStart)
    {
        var source = uxXamlEditor.Editor!.Text;
        var lineStart = source.LastIndexOf('\n', Math.Max(0, elementStart - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var indent = source.Substring(lineStart, elementStart - lineStart);
        if (indent.Any(character => !char.IsWhiteSpace(character))) return "";
        return source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" + indent : "\n" + indent;
    }

    private static string OffsetFragment(string fragment, DesignItem item)
    {
        if (item.View is not Control control) return fragment;
        if (item.Parent?.Component is Canvas)
        {
            fragment = SetRootAttribute(fragment, "Canvas.Left", control.Bounds.X + 8);
            fragment = SetRootAttribute(fragment, "Canvas.Top", control.Bounds.Y + 8);
        }
        else if (item.Parent?.Component is Grid)
        {
            var margin = control.Margin;
            var left = control.HorizontalAlignment switch
            {
                HorizontalAlignment.Right => margin.Left,
                HorizontalAlignment.Center => margin.Left + 16,
                _ => margin.Left + 8
            };
            var right = control.HorizontalAlignment is HorizontalAlignment.Right or HorizontalAlignment.Stretch
                ? margin.Right - 8 : margin.Right;
            var top = control.VerticalAlignment switch
            {
                VerticalAlignment.Bottom => margin.Top,
                VerticalAlignment.Center => margin.Top + 16,
                _ => margin.Top + 8
            };
            var bottom = control.VerticalAlignment is VerticalAlignment.Bottom or VerticalAlignment.Stretch
                ? margin.Bottom - 8 : margin.Bottom;
            fragment = SetRootAttribute(fragment, "Margin", FormattableString.Invariant($"{left:0.###},{top:0.###},{right:0.###},{bottom:0.###}"));
        }
        return fragment;
    }

    private static string SetRootAttribute(string fragment, string name, double value)
        => SetRootAttribute(fragment, name, value.ToString("0.###", CultureInfo.InvariantCulture));

    private static string SetRootAttribute(string fragment, string name, string value)
    {
        var tagEnd = TagEnd(fragment, 0);
        var opening = fragment[..tagEnd];
        var pattern = $"(?<prefix>\\b{Regex.Escape(name)}\\s*=\\s*(?<quote>['\"]))(?<value>.*?)(?<suffix>\\k<quote>)";
        var match = Regex.Match(opening, pattern, RegexOptions.Singleline);
        if (match.Success)
            return fragment[..match.Groups["value"].Index] + value
                + fragment[(match.Groups["value"].Index + match.Groups["value"].Length)..];
        var nameEnd = 1;
        while (nameEnd < opening.Length && !char.IsWhiteSpace(opening[nameEnd]) && opening[nameEnd] is not '/' and not '>') nameEnd++;
        return fragment.Insert(nameEnd, $" {name}=\"{value}\"");
    }

    private void EndMarquee()
    {
        if (_marqueeRect != null)
        {
            SplitResizeOverlay.Children.Remove(_marqueeRect);
            _marqueeRect = null;
        }
        _isMarqueeing = false;
        _isMarqueePending = false;
        _marqueePointer?.Capture(null);
        _marqueePointer = null;
        _marqueeInitialSelection = Array.Empty<DesignItem>();
        _marqueeInitialPrimary = null;
    }

    private void CollectSelectableControls(DesignItem item, List<(DesignItem Item, Rect Bounds)> results)
    {
        if (item.View is Control control)
        {
            var parent = control.GetVisualParent();
            if (parent is Canvas or Grid)
            {
                var transform = control.TransformToVisual(Document?.DesignContext?.RootItem?.View as Visual);
                if (transform.HasValue)
                {
                    var bounds = new Rect(control.Bounds.Size).TransformToAABB(transform.Value);
                    if (bounds.Width > 0 && bounds.Height > 0)
                        results.Add((item, bounds));
                }
            }
        }

        if (item.ContentProperty?.IsCollection == true)
        {
            foreach (var child in item.ContentProperty.CollectionElements)
                CollectSelectableControls(child, results);
        }
        else if (item.ContentProperty?.Value != null)
        {
            CollectSelectableControls(item.ContentProperty.Value, results);
        }
    }

    private bool ApplySourceEdit(int start, int length, string replacement, int elementStart)
    {
        if (Document?.IsPreviewSelectable != true || uxXamlEditor.Editor is not { } editor) return false;
        // Attribute edits keep control order stable, even when preceding attributes change length.
        // Capture before the text notification clears the old preview selection.
        if (_selection is { SelectionCount: > 1 })
        {
            var controls = _sourceControls.OrderBy(entry => entry.Start).Select(entry => entry.Item).ToList();
            var selected = _selection.SelectedItems.Select(item => controls.IndexOf(item)).ToArray();
            var primary = controls.IndexOf(_selection.PrimarySelection);
            if (primary >= 0 && selected.All(index => index >= 0))
                _pendingGroupSelection = (selected, primary, editor.Text.Remove(start, length).Insert(start, replacement));
        }
        _syncingSelection = true;
        try
        {
            using (editor.Document.RunUpdate())
                editor.Document.Replace(start, length, replacement);
            editor.Select(elementStart, 0);
        }
        finally { _syncingSelection = false; }
        return true;
    }

    private void SubscribeSelection(ISelectionService? selection)
    {
        if (Document?.DesignContext is { } context)
        {
            context.Services.AddOrReplaceService(typeof(ISplitResizeOverlayService), this);
            context.Services.AddOrReplaceService(typeof(ISplitModeService), this);
        }
        if (ReferenceEquals(_selection, selection)) return;
        _groupCommands?.Dispose();
        _groupCommands = null;
        if (_selection != null)
        {
            _selection.SelectionChanged -= PreviewSelectionChanged;
            _selection.SetSelectedComponents(Array.Empty<DesignItem>(), SelectionTypes.Replace);
        }
        _selection = selection;
        if (_selection != null) _selection.SelectionChanged += PreviewSelectionChanged;
        if (_selection != null && Document?.DesignContext is { } commandContext)
        {
            _groupCommands = new SplitGroupCommandService(_selection, this, RefreshAfterKeyboardEdit);
            commandContext.Services.AddOrReplaceService(typeof(ISplitGroupCommandService), _groupCommands);
        }
        UpdateGroupCommandBar();
        RebuildSourceControls();
        if (_selection != null && _pendingClipboardSelection is { } clipboardPending)
        {
            _pendingClipboardSelection = null;
            if (Document?.Text == clipboardPending.Source)
            {
                var items = clipboardPending.Starts
                    .Select(start => _sourceControls.FirstOrDefault(entry => entry.Start == start).Item)
                    .ToArray();
                if (items.All(item => item != null))
                {
                    _syncingSelection = true;
                    try { _selection.SetSelectedComponents(items.Cast<DesignItem>().ToArray(), SelectionTypes.Replace); }
                    finally { _syncingSelection = false; }
                    return;
                }
            }
        }
        if (_selection != null && _pendingGroupSelection is { } pending)
        {
            _pendingGroupSelection = null;
            var controls = _sourceControls.OrderBy(entry => entry.Start).Select(entry => entry.Item).ToArray();
            if (Document?.Text == pending.Source && pending.Items.All(index => index < controls.Length)
                && pending.Primary < controls.Length)
            {
                _syncingSelection = true;
                try
                {
                    _selection.SetSelectedComponents(new[] { controls[pending.Primary] }, SelectionTypes.Replace);
                    _selection.SetSelectedComponents(pending.Items.Select(index => controls[index]).ToArray(), SelectionTypes.Replace);
                }
                finally { _syncingSelection = false; }
                return;
            }
        }
        SourceCaretChanged(this, EventArgs.Empty);
    }

    private void RebuildSourceControls()
    {
        _sourceControls.Clear();
        if (_selection == null || Document?.IsPreviewSelectable != true || uxXamlEditor.Editor is not { } editor) return;
        // Match source locations only to controls belonging to this rendered document.
        var context = Document.DesignContext;
        var root = context.RootItem?.View;
        if (root == null) return;
        var models = OutlineItems(Document.OutlineRoot).OfType<XamlDesignItem>().Distinct()
            .Where(i => i.View is Control && i.XamlObject.PositionXmlElement.HasLineInfo() && i.XamlObject.PositionXmlElement.LineNumber > 0)
            .GroupBy(i => (i.XamlObject.PositionXmlElement.LineNumber, i.XamlObject.PositionXmlElement.LinePosition))
            .ToDictionary(g => g.Key, g => (DesignItem)g.First());
        var stack = new Stack<(int Start, DesignItem? Item)>();
        try
        {
            using var reader = XmlReader.Create(new StringReader(Document.Text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var info = (IXmlLineInfo)reader;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    var start = editor.Document.GetOffset(info.LineNumber, info.LinePosition) - 1;
                    models.TryGetValue((info.LineNumber, info.LinePosition), out var item);
                    if (reader.IsEmptyElement)
                    {
                        if (item != null) _sourceControls.Add((start, TagEnd(Document.Text, start), item));
                    }
                    else stack.Push((start, item));
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    var entry = stack.Pop();
                    if (entry.Item != null)
                        _sourceControls.Add((entry.Start, TagEnd(Document.Text, editor.Document.GetOffset(info.LineNumber, info.LinePosition)), entry.Item));
                }
            }
        }
        catch (XmlException) { _sourceControls.Clear(); }
    }

    private static IEnumerable<DesignItem> OutlineItems(Scadix.AxamlDesign.Interfaces.IOutlineNode? node)
    {
        if (node == null) yield break;
        yield return node.DesignItem;
        foreach (var child in node.Children)
            foreach (var item in OutlineItems(child)) yield return item;
    }

    private static int TagEnd(string source, int start)
    {
        char quote = '\0';
        for (var index = start; index < source.Length; index++)
        {
            var c = source[index];
            if (quote != '\0') { if (c == quote) quote = '\0'; }
            else if (c == '\'' || c == '"') quote = c;
            else if (c == '>') return index + 1;
        }
        return source.Length;
    }

    private void SourceCaretChanged(object? sender, EventArgs e)
    {
        if (_syncingSelection || Document?.IsPreviewSelectable != true || _selection == null || uxXamlEditor.Editor is not { } editor) return;
        var offset = editor.SelectionLength > 0 ? editor.SelectionStart : editor.CaretOffset;
        var item = _sourceControls.Where(r => r.Start <= offset && offset < r.End)
            .OrderBy(r => r.End - r.Start).Select(r => r.Item).FirstOrDefault();
        _syncingSelection = true;
        try { _selection.SetSelectedComponents(item == null ? Array.Empty<DesignItem>() : new[] { item }, SelectionTypes.Replace); }
        finally { _syncingSelection = false; }
    }

    private void PreviewSelectionChanged(object? sender, DesignItemCollectionEventArgs e)
    {
        UpdateGroupCommandBar();
        UpdateQuickActionsPanel();
        NavigateToPreviewSelection();
    }

    private void UpdateQuickActionsPanel()
    {
        if (_quickActionsPanel == null || _quickActionsContent == null) return;

        var isSplit = Document?.IsSplitMode == true;
        var isPreviewSelectable = Document?.IsPreviewSelectable == true;
        var hasSelection = _selection?.SelectionCount > 0;

        _quickActionsPanel.IsVisible = isSplit && isPreviewSelectable && hasSelection;

        if (!_quickActionsPanel.IsVisible) return;

        _quickActionsContent.Children.Clear();

        if (_selection == null || _selection.SelectionCount == 0) return;

        var selectedItems = _selection.SelectedItems.ToArray();
        var commonProperties = GetCommonProperties(selectedItems);

        foreach (var propName in _quickActionPropertyNames)
        {
            if (!commonProperties.TryGetValue(propName, out var prop)) continue;

            var control = CreatePropertyEditor(propName, prop, selectedItems);
            if (control != null)
                _quickActionsContent.Children.Add(control);
        }
    }

    private Dictionary<string, DesignItemProperty> GetCommonProperties(IReadOnlyList<DesignItem> items)
    {
        var result = new Dictionary<string, DesignItemProperty>();

        if (items.Count == 1)
        {
            foreach (var propName in _quickActionPropertyNames)
            {
                var prop = TryGetProperty(items[0], propName);
                if (prop != null) result[propName] = prop;
            }
        }
        else
        {
            // Get common properties across all selected items
            foreach (var propName in _quickActionPropertyNames)
            {
                DesignItemProperty? firstProp = null;
                bool allMatch = true;
                foreach (var item in items)
                {
                    var prop = TryGetProperty(item, propName);
                    if (prop == null) { allMatch = false; break; }
                    if (firstProp == null) firstProp = prop;
                    else if (!PropertiesEqual(firstProp, prop)) { allMatch = false; break; }
                }
                if (allMatch && firstProp != null) result[propName] = firstProp;
            }
        }

        return result;
    }

    private bool PropertiesEqual(DesignItemProperty a, DesignItemProperty b)
    {
        try
        {
            var va = a.ValueOnInstance;
            var vb = b.ValueOnInstance;
            if (va == null && vb == null) return true;
            if (va == null || vb == null) return false;
            return va.Equals(vb);
        }
        catch { return false; }
    }

    private static DesignItemProperty? TryGetProperty(DesignItem item, string propertyName)
    {
        try
        {
            return item.Properties.GetProperty(propertyName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private Control? CreatePropertyEditor(string propName, DesignItemProperty prop, IReadOnlyList<DesignItem> items)
    {
        return propName switch
        {
            "HorizontalAlignment" => CreateEnumComboBox(prop, items, typeof(HorizontalAlignment)),
            "VerticalAlignment" => CreateEnumComboBox(prop, items, typeof(VerticalAlignment)),
            "Margin" => CreateThicknessEditor(prop, items),
            "Width" => CreateDoubleEditor(prop, items, "Width"),
            "Height" => CreateDoubleEditor(prop, items, "Height"),
            "Background" => CreateBrushEditor(prop, items, "Background"),
            "Foreground" => CreateBrushEditor(prop, items, "Foreground"),
            "BorderBrush" => CreateBrushEditor(prop, items, "BorderBrush"),
            "BorderThickness" => CreateThicknessEditor(prop, items),
            _ => null
        };
    }

    private Control? CreateEnumComboBox(DesignItemProperty prop, IReadOnlyList<DesignItem> items, Type enumType)
    {
        var comboBox = new ComboBox
        {
            Margin = new Thickness(4),
            MinWidth = 100,
            Tag = prop
        };

        foreach (var value in Enum.GetValues(enumType))
        {
            comboBox.Items.Add(value);
        }

        comboBox.SelectedItem = prop.ValueOnInstance ?? Enum.GetValues(enumType).GetValue(0);
        comboBox.SelectionChanged += (_, _) => SetPropertyValue(prop, items, comboBox.SelectedItem);

        var label = new TextBlock { Text = prop.Name + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        panel.Children.Add(label);
        panel.Children.Add(comboBox);
        return panel;
    }

    private Control? CreateDoubleEditor(DesignItemProperty prop, IReadOnlyList<DesignItem> items, string labelText)
    {
        var textBox = new TextBox
        {
            Margin = new Thickness(4),
            MinWidth = 60,
            Text = prop.ValueOnInstance?.ToString() ?? "",
            Tag = prop
        };

        textBox.LostFocus += (_, _) =>
        {
            if (double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                SetPropertyValue(prop, items, value);
            else
                textBox.Text = prop.ValueOnInstance?.ToString() ?? "";
        };
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                if (double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    SetPropertyValue(prop, items, value);
                else
                    textBox.Text = prop.ValueOnInstance?.ToString() ?? "";
                ((Control)e.Source).Focus(); // Move focus away
            }
        };

        var label = new TextBlock { Text = labelText + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        panel.Children.Add(label);
        panel.Children.Add(textBox);
        return panel;
    }

    private Control? CreateThicknessEditor(DesignItemProperty prop, IReadOnlyList<DesignItem> items)
    {
        var textBox = new TextBox
        {
            Margin = new Thickness(4),
            MinWidth = 120,
            Text = prop.ValueOnInstance?.ToString() ?? "",
            Tag = prop
        };

        textBox.LostFocus += (_, _) =>
        {
            if (TryParseThickness(textBox.Text, out var thickness))
                SetPropertyValue(prop, items, thickness);
            else
                textBox.Text = prop.ValueOnInstance?.ToString() ?? "";
        };
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                if (TryParseThickness(textBox.Text, out var thickness))
                    SetPropertyValue(prop, items, thickness);
                else
                    textBox.Text = prop.ValueOnInstance?.ToString() ?? "";
                ((Control)e.Source).Focus();
            }
        };

        var label = new TextBlock { Text = "Margin:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        panel.Children.Add(label);
        panel.Children.Add(textBox);
        return panel;
    }

    private bool TryParseThickness(string text, out Thickness thickness)
    {
        thickness = default;
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var uniform))
        {
            thickness = new Thickness(uniform);
            return true;
        }
        if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            thickness = new Thickness(h, v, h, v);
            return true;
        }
        if (parts.Length == 4 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var l) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var t) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var r) &&
            double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
        {
            thickness = new Thickness(l, t, r, b);
            return true;
        }
        return false;
    }

    private Control? CreateBrushEditor(DesignItemProperty prop, IReadOnlyList<DesignItem> items, string labelText)
    {
        var textBox = new TextBox
        {
            Margin = new Thickness(4),
            MinWidth = 100,
            Text = prop.ValueOnInstance?.ToString() ?? "",
            Tag = prop
        };

        textBox.LostFocus += (_, _) =>
        {
            if (TryParseBrush(textBox.Text, out var brush))
                SetPropertyValue(prop, items, brush);
            else
                textBox.Text = prop.ValueOnInstance?.ToString() ?? "";
        };
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                if (TryParseBrush(textBox.Text, out var brush))
                    SetPropertyValue(prop, items, brush);
                else
                    textBox.Text = prop.ValueOnInstance?.ToString() ?? "";
                ((Control)e.Source).Focus();
            }
        };

        var label = new TextBlock { Text = labelText + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        panel.Children.Add(label);
        panel.Children.Add(textBox);
        return panel;
    }

    private bool TryParseBrush(string text, out IBrush? brush)
    {
        brush = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            // Try to parse as color (e.g., "Red", "#FF0000", "rgb(255,0,0)")
            var color = Color.Parse(text);
            brush = new SolidColorBrush(color);
            return true;
        }
        catch
        {
            // Could also support {DynamicResource ...} and {StaticResource ...} strings
            // For now, just return false for complex brushes
            return false;
        }
    }

    private void SetPropertyValue(DesignItemProperty prop, IReadOnlyList<DesignItem> items, object value)
    {
        if (prop == null || items.Count == 0 || uxXamlEditor.Editor is not { } editor) return;
        var formatted = FormatPropertyValue(value);
        var replacements = new List<(int Start, int Length, string Text)>();
        foreach (var item in items)
        {
            var entry = _sourceControls.FirstOrDefault(candidate => ReferenceEquals(candidate.Item, item));
            if (entry.Item == null || TryGetProperty(item, prop.Name) == null
                || IsProtectedSourceExpression(item, prop.Name)) return;
            var openingEnd = TagEnd(editor.Text, entry.Start);
            var opening = editor.Text.Substring(entry.Start, openingEnd - entry.Start);
            var replacement = SetRootAttribute(opening, prop.Name, formatted);
            if (replacement != opening)
                replacements.Add((entry.Start, opening.Length, replacement));
        }
        if (replacements.Count == 0) return;

        var source = editor.Text;
        foreach (var change in replacements.OrderByDescending(change => change.Start))
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        CapturePendingSelection(source);
        _syncingSelection = true;
        try
        {
            using (editor.Document.RunUpdate())
            {
                foreach (var change in replacements.OrderByDescending(change => change.Start))
                    editor.Document.Replace(change.Start, change.Length, change.Text);
            }
            editor.Select(replacements.Min(change => change.Start), 0);
        }
        finally { _syncingSelection = false; }
        RefreshAfterKeyboardEdit();
    }

    private void CapturePendingSelection(string source)
    {
        if (_selection is not { SelectionCount: > 0 }) return;
        var controls = _sourceControls.OrderBy(entry => entry.Start).Select(entry => entry.Item).ToList();
        var selected = _selection.SelectedItems.Select(item => controls.IndexOf(item)).ToArray();
        var primary = controls.IndexOf(_selection.PrimarySelection);
        if (primary >= 0 && selected.All(index => index >= 0))
            _pendingGroupSelection = (selected, primary, source);
    }

    private static string FormatPropertyValue(object value) => value switch
    {
        double number => number.ToString("0.###", CultureInfo.InvariantCulture),
        Thickness thickness => FormattableString.Invariant(
            $"{thickness.Left:0.###},{thickness.Top:0.###},{thickness.Right:0.###},{thickness.Bottom:0.###}"),
        SolidColorBrush brush => brush.Color.ToString(),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private void OnPreviewDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Document?.IsPreviewSelectable != true || _selection?.PrimarySelection == null) return;

        var primary = _selection.PrimarySelection;
        if (primary.View is not Control control) return;

        // Find Text or Content property
        var textProp = TryGetProperty(primary, "Text");
        var contentProp = TryGetProperty(primary, "Content");

        DesignItemProperty? editProp = null;
        if (textProp != null && textProp.IsSet && !IsProtectedSourceExpression(primary, "Text")) editProp = textProp;
        else if (contentProp != null && contentProp.IsSet && !IsProtectedSourceExpression(primary, "Content")) editProp = contentProp;

        if (editProp == null)
        {
            // Show warning for bindings or resource references
            if ((textProp != null && textProp.IsSet && IsProtectedSourceExpression(primary, "Text"))
                || (contentProp != null && contentProp.IsSet && IsProtectedSourceExpression(primary, "Content")))
            {
                ShowExpressionWarning();
            }
            return;
        }

        StartInlineEdit(primary, editProp, control);
        e.Handled = true;
    }

    private void ShowExpressionWarning()
    {
        // Show a simple toast/warning - could be enhanced to open expression editor later
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // For now, just log to debug output - could show a notification/toast in future
        System.Diagnostics.Debug.WriteLine("Cannot edit binding or resource reference directly. Use Property Grid or XAML editor.");
    }

    private bool IsBinding(DesignItemProperty prop)
    {
        try
        {
            var textValue = prop.TextValue;
            return !string.IsNullOrEmpty(textValue) && textValue.TrimStart().StartsWith('{') && !textValue.StartsWith("{}", StringComparison.Ordinal);
        }
        catch { return false; }
    }

    private bool IsResourceReference(DesignItemProperty prop)
    {
        try
        {
            var textValue = prop.TextValue;
            if (string.IsNullOrEmpty(textValue)) return false;
            var trimmed = textValue.TrimStart();
            return trimmed.StartsWith("{StaticResource", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("{DynamicResource", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private bool IsProtectedSourceExpression(DesignItem item, string propertyName)
    {
        if (!TryGetSourceAttribute(item, propertyName, out _, out _, out var value, out _)) return false;
        var trimmed = value.TrimStart();
        return trimmed.StartsWith('{') && !trimmed.StartsWith("{}", StringComparison.Ordinal);
    }

    private bool TryGetSourceAttribute(DesignItem item, string propertyName, out int valueStart,
        out int valueLength, out string value, out char quote)
    {
        valueStart = valueLength = 0;
        value = string.Empty;
        quote = '\0';
        if (uxXamlEditor.Editor is not { } editor) return false;
        var entry = _sourceControls.FirstOrDefault(candidate => ReferenceEquals(candidate.Item, item));
        if (entry.Item == null) return false;
        var openingEnd = TagEnd(editor.Text, entry.Start);
        if (openingEnd <= entry.Start) return false;
        var opening = editor.Text.Substring(entry.Start, openingEnd - entry.Start);
        var pattern = $"(?<prefix>\\b{Regex.Escape(propertyName)}\\s*=\\s*(?<quote>['\"]))(?<value>.*?)(?<suffix>\\k<quote>)";
        var match = Regex.Match(opening, pattern, RegexOptions.Singleline);
        if (!match.Success) return false;
        var group = match.Groups["value"];
        valueStart = entry.Start + group.Index;
        valueLength = group.Length;
        value = group.Value;
        quote = match.Groups["quote"].Value[0];
        return true;
    }

    private void StartInlineEdit(DesignItem item, DesignItemProperty prop, Control control)
    {
        if (_inlineEditOverlay == null) return;

        _inlineEditItem = item;
        _inlineEditProperty = prop;

        // Create TextBox overlay
        _inlineEditTextBox = new TextBox
        {
            Text = prop.ValueOnInstance?.ToString() ?? "",
            FontSize = GetFontSize(control),
            FontFamily = GetFontFamily(control),
            Foreground = GetForeground(control),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = this.FindResource("SystemAccentColor") is Color accent
                ? new SolidColorBrush(accent)
                : Brushes.DodgerBlue,
            Padding = new Thickness(2),
            AcceptsReturn = false
        };

        // Position overlay over the control
        var bounds = control.Bounds;
        var transform = control.TransformToVisual(_inlineEditOverlay);
        if (transform.HasValue)
        {
            var topLeft = transform.Value.Transform(new Point(0, 0));
            Canvas.SetLeft(_inlineEditTextBox, topLeft.X);
            Canvas.SetTop(_inlineEditTextBox, topLeft.Y);
            _inlineEditTextBox.Width = bounds.Width;
            _inlineEditTextBox.Height = bounds.Height;
        }

        _inlineEditOverlay.Children.Clear();
        _inlineEditOverlay.Children.Add(_inlineEditTextBox);
        _inlineEditOverlay.IsVisible = true;

        _inlineEditTextBox.Focus();
        _inlineEditTextBox.SelectAll();

        _inlineEditTextBox.LostFocus += OnInlineEditLostFocus;
        _inlineEditTextBox.KeyDown += OnInlineEditKeyDown;

        _inlineEditCommitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _inlineEditCommitTimer.Tick += (_, _) =>
        {
            _inlineEditCommitTimer?.Stop();
            CommitInlineEdit();
        };
    }

    private void OnInlineEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitInlineEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelInlineEdit();
            e.Handled = true;
        }
    }

    private void OnInlineEditLostFocus(object? sender, RoutedEventArgs e)
    {
        // Delay commit to allow clicking away
        _inlineEditCommitTimer?.Start();
    }

    private void CommitInlineEdit()
    {
        if (_inlineEditTextBox == null || _inlineEditItem == null || _inlineEditProperty == null) return;

        var editedItem = _inlineEditItem;
        var propertyName = _inlineEditProperty.Name;
        var committed = TryGetSourceAttribute(editedItem, propertyName, out var start, out var length,
            out var oldValue, out var quote);
        if (committed)
        {
            var newValue = EscapeAttributeValue(_inlineEditTextBox.Text ?? string.Empty, quote);
            committed = newValue == oldValue || ApplySourceEdit(start, length, newValue,
                _sourceControls.First(entry => ReferenceEquals(entry.Item, editedItem)).Start);
        }

        CancelInlineEdit();
        if (committed) RefreshAfterKeyboardEdit();
    }

    private static string EscapeAttributeValue(string value, char quote)
    {
        var escaped = value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
        return quote == '\''
            ? escaped.Replace("'", "&apos;", StringComparison.Ordinal)
            : escaped.Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private void CancelInlineEdit()
    {
        if (_inlineEditOverlay != null)
        {
            _inlineEditOverlay.Children.Clear();
            _inlineEditOverlay.IsVisible = false;
        }
        if (_inlineEditTextBox != null)
        {
            _inlineEditTextBox.LostFocus -= OnInlineEditLostFocus;
            _inlineEditTextBox.KeyDown -= OnInlineEditKeyDown;
            _inlineEditTextBox = null;
        }
        _inlineEditItem = null;
        _inlineEditProperty = null;
        _inlineEditCommitTimer?.Stop();
        _inlineEditCommitTimer = null;
    }

    private double GetFontSize(Control control)
    {
        return control.GetValue(TextBlock.FontSizeProperty);
    }

    private FontFamily GetFontFamily(Control control)
    {
        return control.GetValue(TextBlock.FontFamilyProperty);
    }

    private IBrush GetForeground(Control control)
    {
        return control.GetValue(TextBlock.ForegroundProperty);
    }

    private void UpdateGroupCommandBar()
    {
        var canAlign = Document?.IsSplitMode == true && _groupCommands?.CanAlign == true;
        var canDistribute = canAlign && _groupCommands?.CanDistribute == true;
        SplitGroupCommandBar.IsVisible = canAlign;
        foreach (var button in SplitGroupCommandBar.Children.OfType<Button>())
        {
            var enabled = button == DistributeHorizontalButton || button == DistributeVerticalButton
                ? canDistribute : canAlign;
            button.IsEnabled = enabled;
            button.IsVisible = enabled;
        }
    }

    private void AlignLeft_Click(object? sender, RoutedEventArgs e) => _groupCommands?.Align(GroupAlignment.Left);
    private void AlignHorizontalCenter_Click(object? sender, RoutedEventArgs e) => _groupCommands?.Align(GroupAlignment.HorizontalCenter);
    private void AlignRight_Click(object? sender, RoutedEventArgs e) => _groupCommands?.Align(GroupAlignment.Right);
    private void AlignTop_Click(object? sender, RoutedEventArgs e) => _groupCommands?.Align(GroupAlignment.Top);
    private void AlignVerticalCenter_Click(object? sender, RoutedEventArgs e) => _groupCommands?.Align(GroupAlignment.VerticalCenter);
    private void AlignBottom_Click(object? sender, RoutedEventArgs e) => _groupCommands?.Align(GroupAlignment.Bottom);
    private void DistributeHorizontal_Click(object? sender, RoutedEventArgs e) => _groupCommands?.Distribute(GroupDistribution.Horizontal);
    private void DistributeVertical_Click(object? sender, RoutedEventArgs e) => _groupCommands?.Distribute(GroupDistribution.Vertical);

    private void NavigateToPreviewSelection()
    {
        if (_syncingSelection || Document?.IsPreviewSelectable != true || _selection?.PrimarySelection is not XamlDesignItem item)
            return;
        var editor = uxXamlEditor.Editor;
        var element = item.XamlObject.PositionXmlElement;
        if (editor == null || !element.HasLineInfo() || element.LineNumber < 1 || element.LineNumber > editor.Document.LineCount) return;
        var offset = editor.Document.GetOffset(element.LineNumber, element.LinePosition);
        // XML line positions point at the element name, immediately after '<'.
        var start = Document.Text.LastIndexOf('<', Math.Min(offset, Document.Text.Length - 1));
        if (start < 0) return;
        char quote = '\0';
        for (var end = start + 1; end < Document.Text.Length; end++)
        {
            var c = Document.Text[end];
            if (quote != '\0') { if (c == quote) quote = '\0'; }
            else if (c == '\'' || c == '"') quote = c;
            else if (c == '>')
            {
                _syncingSelection = true;
                try
                {
                    editor.Select(start, end - start + 1);
                    editor.ScrollTo(element.LineNumber, element.LinePosition);
                }
                finally { _syncingSelection = false; }
                return;
            }
        }
    }

    private void UpdateLayoutMode()
    {
        if (Document == null) return;
        UpdateGroupCommandBar();
        var columns = EditorLayout.ColumnDefinitions;
        if (_wasSplit)
        {
            _editorWidth = columns[0].Width;
            _previewWidth = columns[2].Width;
        }
        _wasSplit = Document.IsSplitMode;
        columns[0].Width = Document.IsSplitMode ? _editorWidth : Document.IsEditorVisible ? GridLength.Star : new GridLength(0);
        columns[1].Width = new GridLength(Document.IsSplitMode ? 6 : 0);
        columns[2].Width = Document.IsSplitMode ? _previewWidth : Document.IsPreviewVisible ? GridLength.Star : new GridLength(0);
        columns[0].MinWidth = Document.IsSplitMode ? 120 : 0;
        columns[2].MinWidth = Document.IsSplitMode ? 120 : 0;
    }

    /// <summary>
    /// Switch to XAML mode and jump to the error position.
    /// Called by Shell.JumpToError.
    /// </summary>
    public void JumpToError(XamlError error)
    {
        if (Document == null) return;
        if (!Document.IsSplitMode) Document.Mode = DocumentMode.Xaml;
        uxXamlEditor.JumpToError(error);
    }

    /// <summary>
    /// Set the execution line indicator (yellow arrow + background) in the editor.
    /// Called externally if needed. Pass -1 to clear.
    /// The XamlEditorView already handles this automatically via DebugToolbarViewModel events,
    /// but this method is kept for explicit control (e.g. from Shell).
    /// </summary>
    public void SetCurrentExecutionLine(int line)
    {
        uxXamlEditor.SetCurrentExecutionLine(line);
        if (line > 0)
        {
            // Switch to XAML mode so the arrow is visible
            if (Document?.IsSplitMode != true) Document!.Mode = DocumentMode.Xaml;
        }
    }
}
