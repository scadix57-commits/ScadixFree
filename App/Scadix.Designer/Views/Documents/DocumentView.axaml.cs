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

namespace Scadix.Designer;

public partial class DocumentView : UserControl
{
    public Document? Document { get; private set; }
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private GridLength _editorWidth = new(1, GridUnitType.Star);
    private GridLength _previewWidth = new(1, GridUnitType.Star);
    private bool _wasSplit;
    private bool _subscribed;
    private ISelectionService? _selection;


    public DocumentView()
    {
        InitializeComponent();
        this.Loaded += DocumentView_Loaded;
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); Document?.RefreshPreview(); };
        AttachedToVisualTree += (_, _) => Subscribe();
        DetachedFromVisualTree += (_, _) =>
        {
            _previewTimer.Stop();
            if (Document != null && _subscribed) Document.PropertyChanged -= DocumentChanged;
            _subscribed = false;
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
        Document.PropertyChanged += DocumentChanged;
        _subscribed = true;
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
        Document.SelectionService!.SetSelectedComponents(selected == null
            ? Array.Empty<DesignItem>() : new[] { selected }, SelectionTypes.Replace);
    }

    private void SubscribeSelection(ISelectionService? selection)
    {
        if (_selection != null) _selection.SelectionChanged -= PreviewSelectionChanged;
        _selection = selection;
        if (_selection != null) _selection.SelectionChanged += PreviewSelectionChanged;
    }

    private void PreviewSelectionChanged(object? sender, DesignItemCollectionEventArgs e)
    {
        if (Document?.IsPreviewSelectable != true || _selection?.PrimarySelection is not XamlDesignItem item)
            return;
        var editor = uxXamlEditor.Editor;
        var element = item.XamlObject.PositionXmlElement;
        if (editor == null || element.LineNumber < 1 || element.LineNumber > editor.Document.LineCount) return;
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
                editor.Select(start, end - start + 1);
                editor.ScrollTo(element.LineNumber, element.LinePosition);
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
