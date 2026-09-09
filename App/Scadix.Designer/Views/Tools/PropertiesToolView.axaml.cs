using System;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Interfaces;
using Scadix.AxamlDesigner.PropertyGrid;

namespace Scadix.Designer;

/// <summary>
/// Wrapper for PropertyGridView that wires SelectionChanged events manually.
///
/// WHY NOT XAML BINDING:
/// PropertyGridView sets DataContext = its own PropertyGrid instance in its
/// constructor, so any external DataContext binding is overwritten.
/// We must wire SelectedItems imperatively via SelectionChanged events.
/// </summary>
public partial class PropertiesToolView : UserControl
{
    private ISelectionService? _currentSelection;
    private Document? _document;

    public PropertiesToolView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
    }

    private void OnAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Subscribe to Shell.CurrentDocument changes
        MainWindowViewModel.Instance.PropertyChanged += OnShellPropertyChanged;

        // Push the current document immediately (in case already set)
        SwitchDocument(MainWindowViewModel.Instance.CurrentDocument);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        MainWindowViewModel.Instance.PropertyChanged -= OnShellPropertyChanged;
        UnsubscribeSelection();
        if (_document != null) _document.PropertyChanged -= OnDocumentPropertyChanged;
        _document = null;
        base.OnDetachedFromVisualTree(e);
    }

    // ── Shell.CurrentDocument changed ────────────────────────────────────

    private void OnShellPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentDocument))
            SwitchDocument(MainWindowViewModel.Instance.CurrentDocument);
    }

    private void SwitchDocument(Document? doc)
    {
        UnsubscribeSelection();
        if (_document != null) _document.PropertyChanged -= OnDocumentPropertyChanged;
        _document = doc;
        UpdateEditingState();

        if (doc == null)
        {
            PushSelection(null);
            return;
        }

        // Subscribe to document property changes so we catch Mode → Design
        doc.PropertyChanged += OnDocumentPropertyChanged;

        // Push current selection (may be null if not in Design mode yet)
        SubscribeAndPushSelection(doc);
    }

    private void OnDocumentPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateEditingState();
        if (e.PropertyName == nameof(Document.SelectionService))
            SubscribeAndPushSelection(MainWindowViewModel.Instance.CurrentDocument);
    }

    private void UpdateEditingState()
    {
        var grid = this.FindControl<PropertyGridView>("uxPropertyGridView");
        if (grid != null) grid.IsReadOnly = _document?.IsDesignerInteractive != true;
    }

    // ── Selection wiring ─────────────────────────────────────────────────

    private void SubscribeAndPushSelection(Document? doc)
    {
        UnsubscribeSelection();

        var sel = doc?.SelectionService;
        if (sel == null)
        {
            PushSelection(null);
            return;
        }

        _currentSelection = sel;
        sel.SelectionChanged += OnSelectionChanged;

        // Push the current selection immediately
        PushSelection(sel.SelectedItems);
    }

    private void UnsubscribeSelection()
    {
        if (_currentSelection != null)
        {
            _currentSelection.SelectionChanged -= OnSelectionChanged;
            _currentSelection = null;
        }
    }

    private void OnSelectionChanged(object? sender, DesignItemCollectionEventArgs e)
    {
        PushSelection(_currentSelection?.SelectedItems);
    }

    private void PushSelection(System.Collections.Generic.IEnumerable<DesignItem>? items)
    {
        var pgv = this.FindControl<PropertyGridView>("uxPropertyGridView");
        if (pgv != null)
            pgv.SelectedItems = items!;
    }

    // ── Public accessor ──────────────────────────────────────────────────

    /// <summary>Expose the inner IPropertyGrid for Shell.PropertyGrid wiring.</summary>
    public IPropertyGrid? PropertyGrid =>
        this.FindControl<PropertyGridView>("uxPropertyGridView")?.PropertyGrid;
}
