using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Interactivity;
using Avalonia.Input;
using Scadix.AxamlDesigner.Services;
using Scadix.Designer.Services;
using Scadix.Designer.ViewModels.Tools;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Xaml;
using Avalonia;
using System.IO;
using System.Linq;
using System.Xml;

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
        SplitResizeOverlay.KeyDown += OnMarqueeKeyDown;

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

        // Non-XAML files → editor only; XAML/AXAML → Design mode
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

    private void OnMarqueeKeyDown(object? sender, KeyEventArgs e)
    {
        if (_isMarqueeing && e.Key == Key.Escape)
        {
            EndMarquee();
            e.Handled = true;
        }
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
        NavigateToPreviewSelection();
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
