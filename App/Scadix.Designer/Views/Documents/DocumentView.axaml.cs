using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using Avalonia.Interactivity;
using Scadix.AxamlDesigner.Services;
using Scadix.Designer.Services;
using Scadix.Designer.ViewModels.Tools;
using Avalonia.Input;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Xaml;
using Avalonia;
using Avalonia.VisualTree;
using System.Collections.Generic;
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
    private bool _syncingSelection;
    private readonly List<(int Start, int End, DesignItem Item)> _sourceControls = new();

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

    public void RefreshAfterKeyboardEdit()
    {
        _previewTimer.Stop();
        Document?.RefreshPreview();
        Document?.DesignSurface.UpdateLayout();
    }

    public DocumentView()
    {
        InitializeComponent();
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
            UpdateLayoutMode();
        }
        else if (e.PropertyName == nameof(Document.Text) && Document?.IsSplitMode == true)
        {
            _previewTimer.Stop();
            _previewTimer.Start();
        }
    }

    private void PreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
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
        Document.SelectionService!.SetSelectedComponents(selected == null
            ? Array.Empty<DesignItem>() : new[] { selected }, SelectionTypes.Replace);
        if (alreadySelected) NavigateToPreviewSelection();
        if (selected != null) SplitResizeOverlay.Focus();
    }

    private bool ApplySourceEdit(int start, int length, string replacement, int elementStart)
    {
        if (Document?.IsPreviewSelectable != true || uxXamlEditor.Editor is not { } editor) return false;
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
        if (_selection != null)
        {
            _selection.SelectionChanged -= PreviewSelectionChanged;
            _selection.SetSelectedComponents(Array.Empty<DesignItem>(), SelectionTypes.Replace);
        }
        _selection = selection;
        if (_selection != null) _selection.SelectionChanged += PreviewSelectionChanged;
        RebuildSourceControls();
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
        => NavigateToPreviewSelection();

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
