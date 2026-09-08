using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Scadix.AxamlDesign;

namespace Scadix.Designer.ViewModels.Tools;

// ── Node wrapper ──────────────────────────────────────────────────────────────
// Wraps a DesignItem to provide observable expand/select state
// without touching the core design model.
public partial class LogicalNode : ObservableObject
{
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isVisible = true;  // for search filtering

    public DesignItem DesignItem { get; }
    public string DisplayName { get; }
    public string TypeName    { get; }
    public string Icon        { get; }

    public ObservableCollection<LogicalNode> Children { get; } = new();

    public LogicalNode(DesignItem item)
    {
        DesignItem  = item;
        TypeName    = item.ComponentType?.Name ?? "Unknown";
        DisplayName = !string.IsNullOrWhiteSpace(item.Name) ? $"{item.Name}" : TypeName;
        Icon        = ResolveIcon(TypeName);

        // Use AllSetProperties — the only safely enumerable property collection
        // A property can be either a single DesignItem (Value) or a collection (CollectionElements)
        foreach (var prop in item.AllSetProperties)
        {
            if (prop.IsCollection)
            {
                foreach (var el in prop.CollectionElements)
                    Children.Add(new LogicalNode(el));
            }
            else if (prop.Value is { } childItem)
            {
                Children.Add(new LogicalNode(childItem));
            }
        }
    }

    // ── Depth-first search for filtering ─────────────────────────────────────

    public bool ApplyFilter(string query)
    {
        bool childMatch = Children.Any(c => c.ApplyFilter(query));
        bool selfMatch  = string.IsNullOrWhiteSpace(query)
                          || DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                          || TypeName.Contains(query, StringComparison.OrdinalIgnoreCase);

        IsVisible   = selfMatch || childMatch;
        IsExpanded  = childMatch && !string.IsNullOrWhiteSpace(query);
        return IsVisible;
    }

    // ── Icon picker: maps common Avalonia type names to path icons ────────────

    private static string ResolveIcon(string typeName) => typeName switch
    {
        "Grid"           => "M3 3h7v7H3zm0 11h7v7H3zm11-11h7v7h-7zm0 11h7v7h-7z",
        "StackPanel"     => "M3 5h18v2H3zm0 6h18v2H3zm0 6h18v2H3z",
        "DockPanel"      => "M3 3h18v4H3zM3 10h4v11H3zM17 10h4v11h-4zM3 18h18v3H3z",
        "Border"         => "M3 3h18v18H3V3zm2 2v14h14V5H5z",
        "TextBlock"      => "M5 5h14v2H5zm0 4h14v2H5zm0 4h10v2H5z",
        "TextBox"        => "M3 6h18v12H3V6zm2 2v8h14V8H5zm1 1h3v2H6zm0 3h8v1H6z",
        "Button"         => "M6 8h12a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-4a2 2 0 0 1 2-2z",
        "Image"          => "M21 19V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2zM8.5 13.5l2.5 3 3.5-4.5 4.5 6H5l3.5-4.5z",
        "ListBox"        => "M3 5h2v2H3zm4 0h14v2H7zM3 11h2v2H3zm4 0h14v2H7zM3 17h2v2H3zm4 0h14v2H7z",
        "ScrollViewer"   => "M4 4h16v16H4V4zm14 10h-3v3h-2v-3H9v-2h4V9h2v3h3v2z",
        "Canvas"         => "M3 3h18v18H3V3zm16 16V5H5v14h14z",
        "WrapPanel"      => "M3 5h8v6H3zm10 0h8v6h-8zM3 13h5v6H3zm7 0h11v6H10z",
        "TabControl"     => "M3 3h8v3H3zm10 0h8v3h-8zM3 6h18v15H3V6z",
        "ComboBox"       => "M3 6h18v4H3zm0 0l9 6 9-6",
        "CheckBox"       => "M19 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2zm-9 14l-5-5 1.4-1.4L10 14.2l7.6-7.6L19 8l-9 9z",
        "Slider"         => "M5 13h14v-2H5zm7-9a2 2 0 1 0 0 4 2 2 0 0 0 0-4z",
        _                => "M12 2L2 7l10 5 10-5zm0 7L2 14l10 5 10-5zm0 7L2 19l10 5 10-5z", // generic layers
    };
}

// ── LogicalTreeViewModel ──────────────────────────────────────────────────────
// Design philosophy:
//   • The tree is rebuilt lazily when a new root is set — no live subscription
//     to design model events to avoid tight coupling.
//   • Filter is applied client-side on the node wrapper tree, not by re-querying
//     the design model, so it's always instant.
//   • Selection syncs bidirectionally with the design surface via the
//     ISelectionService interface, but gracefully degrades when no designer is open.
public partial class LogicalTreeViewModel : Tool
{
    // ── State ─────────────────────────────────────────────────────────────────

    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private LogicalNode? _selectedNode;
    [ObservableProperty] private bool _hasContent;
    [ObservableProperty] private bool _isFiltering;
    [ObservableProperty] private int _totalNodes;
    [ObservableProperty] private int _visibleNodes;

    public ObservableCollection<LogicalNode> RootNodes { get; } = new();

    // External selection service — set by the design surface when activated.
    private ISelectionService? _selectionService;

    public LogicalTreeViewModel()
    {
        Id       = "LogicalTree";
        Title    = "Logical Tree";
        CanClose = true;
    }

    // ── Search ────────────────────────────────────────────────────────────────

    partial void OnSearchQueryChanged(string value)
    {
        IsFiltering = !string.IsNullOrWhiteSpace(value);
        ApplyFilter(value);
        UpdateCounts();
    }

    private void ApplyFilter(string query)
    {
        foreach (var root in RootNodes)
            root.ApplyFilter(query);
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    partial void OnSelectedNodeChanged(LogicalNode? value)
    {
        if (value == null || _selectionService == null) return;
        _selectionService.SetSelectedComponents(new[] { value.DesignItem });
    }

    // Sync from design surface → tree when external selection changes.
    private void OnExternalSelectionChanged(object? sender, DesignItemCollectionEventArgs e)
    {
        var primary = _selectionService?.PrimarySelection;
        if (primary == null) return;

        var match = FindNode(RootNodes, primary);
        if (match != null && match != SelectedNode)
        {
            // Switch to UI thread — design surface may call from any thread.
            Dispatcher.UIThread.Post(() =>
            {
                SelectedNode = match;
                ExpandPathTo(match);
            });
        }
    }

    private static LogicalNode? FindNode(IEnumerable<LogicalNode> nodes, DesignItem target)
    {
        foreach (var n in nodes)
        {
            if (n.DesignItem == target) return n;
            var found = FindNode(n.Children, target);
            if (found != null) return found;
        }
        return null;
    }

    private static void ExpandPathTo(LogicalNode node)
    {
        // Walk up the logical parent chain to ensure node is visible.
        // (Full parent-chain walk requires a parent reference — kept simple here.)
        node.IsExpanded = true;
    }

    // ── Tree population ───────────────────────────────────────────────────────

    /// <summary>
    /// Called by the active designer view whenever the root DesignItem changes
    /// (new file opened, designer reloaded, etc.).
    /// </summary>
    public void SetRoot(DesignItem? root, ISelectionService? selectionService)
    {
        // Detach old service
        if (_selectionService != null)
            _selectionService.SelectionChanged -= OnExternalSelectionChanged;

        _selectionService = selectionService;

        if (_selectionService != null)
            _selectionService.SelectionChanged += OnExternalSelectionChanged;

        Dispatcher.UIThread.Post(() =>
        {
            RootNodes.Clear();
            SelectedNode = null;
            SearchQuery  = "";

            if (root != null)
            {
                var node = new LogicalNode(root);
                RootNodes.Add(node);
                HasContent = true;
            }
            else
            {
                HasContent = false;
            }

            UpdateCounts();
        });
    }

    public void Clear()
    {
        SetRoot(null, null);
    }

    // ── Toolbar Commands ──────────────────────────────────────────────────────

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in RootNodes)
            SetExpanded(node, false);
    }

    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var node in RootNodes)
            SetExpanded(node, true);
    }

    [RelayCommand]
    private void ClearSearch() => SearchQuery = "";

    private static void SetExpanded(LogicalNode node, bool value)
    {
        node.IsExpanded = value;
        foreach (var child in node.Children)
            SetExpanded(child, value);
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    private void UpdateCounts()
    {
        var all = FlattenNodes(RootNodes).ToList();
        TotalNodes   = all.Count;
        VisibleNodes = all.Count(n => n.IsVisible);
    }

    private static IEnumerable<LogicalNode> FlattenNodes(IEnumerable<LogicalNode> nodes)
    {
        foreach (var n in nodes)
        {
            yield return n;
            foreach (var c in FlattenNodes(n.Children))
                yield return c;
        }
    }
}
