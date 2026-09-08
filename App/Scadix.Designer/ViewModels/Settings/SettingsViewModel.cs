using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace Scadix.Designer.ViewModels;

// ── Settings page node ────────────────────────────────────────────────────────
public partial class SettingsPageNode : ObservableObject
{
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string iconKey = "EditIcon";
    [ObservableProperty] private bool isExpanded = false;
    [ObservableProperty] private bool isSelected = false;
    [ObservableProperty] private bool isVisible = true;

    public ObservableCollection<SettingsPageNode> Children { get; } = new();
    public bool HasChildren => Children.Count > 0;

    /// <summary>Unique page id used to switch content panel.</summary>
    public string PageId { get; init; } = "";
}

// ── Main ViewModel ────────────────────────────────────────────────────────────
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private SettingsPageNode? selectedPage;
    [ObservableProperty] private string currentPageId = "appearance";

    public ObservableCollection<SettingsPageNode> Pages { get; } = new();

    public SettingsViewModel()
    {
        BuildTree();
        // select first leaf
        SelectedPage = Pages.FirstOrDefault();
        if (SelectedPage != null) CurrentPageId = SelectedPage.PageId;
    }

    partial void OnSearchTextChanged(string value) => FilterTree(value);

    partial void OnSelectedPageChanged(SettingsPageNode? value)
    {
        if (value != null && !value.HasChildren)
            CurrentPageId = value.PageId;
    }

    private void FilterTree(string query)
    {
        query = query.Trim().ToLowerInvariant();
        foreach (var node in Pages)
            ApplyFilter(node, query);
    }

    private static bool ApplyFilter(SettingsPageNode node, string query)
    {
        bool selfMatch = string.IsNullOrEmpty(query) ||
                         node.Title.ToLowerInvariant().Contains(query);

        bool childMatch = false;
        foreach (var child in node.Children)
            childMatch |= ApplyFilter(child, query);

        node.IsVisible = selfMatch || childMatch;
        if (childMatch && !string.IsNullOrEmpty(query))
            node.IsExpanded = true;

        return node.IsVisible;
    }

    private void BuildTree()
    {
        Pages.Add(Group("Appearance & Behavior", "SolutionIcon", "appearance_root",
            Leaf("Appearance",        "EditIcon",       "appearance"),
            Leaf("Menus & Toolbars",  "ListIcon",       "menus"),
            Leaf("System Settings",   "ConfigFileIcon", "system"),
            Leaf("Notifications",     "WarningIcon",    "notifications")
        ));

        Pages.Add(Group("Keymap", "PointerIcon", "keymap_root",
            Leaf("Keymap",            "PointerIcon",    "keymap")
        ));

        Pages.Add(Group("Editor", "TextBoxIcon", "editor_root",
            Leaf("General",           "EditIcon",       "editor_general"),
            Leaf("Font",              "ABCIcon",        "editor_font"),
            Leaf("Color Scheme",      "GridIcon",       "editor_colors"),
            Leaf("Code Style",        "TextBoxIcon",    "editor_codestyle"),
            Leaf("File Encodings",    "TextFileIcon",   "editor_encoding")
        ));

        Pages.Add(Leaf("Plugins", "PackageIcon", "plugins"));

        Pages.Add(Group("Build, Execution, Deployment", "BuildIcon", "build_root",
            Leaf("Build",             "BuildIcon",      "build"),
            Leaf("Compiler",          "ConvertIcon",    "compiler"),
            Leaf("Debugger",          "ErrorIcon",      "debugger")
        ));

        Pages.Add(Group("Version Control", "TreeViewIcon", "vcs_root",
            Leaf("Git",               "TreeViewIcon",   "git"),
            Leaf("Commit",            "SaveIcon",       "commit"),
            Leaf("GitHub",            "TreeViewIcon",   "github")
        ));

        Pages.Add(Group("Languages & Frameworks", "FrameworkIcon", "langs_root",
            Leaf("C#",                "CSharpFileIcon", "csharp"),
            Leaf("XAML / Avalonia",   "XamlFileIcon",   "xaml"),
            Leaf("JSON",              "TextFileIcon",   "json")
        ));

        Pages.Add(Group("Tools", "ConfigFileIcon", "tools_root",
            Leaf("Terminal",          "TextBoxIcon",    "terminal"),
            Leaf("NuGet",             "PackageIcon",    "nuget"),
            Leaf("Database",          "GridIcon",       "database"),
            Leaf("External Tools",    "BuildIcon",      "external_tools")
        ));

        Pages.Add(Group("Designer", "XamlFileIcon", "designer_root",
            Leaf("Parsing Engine",    "ConvertIcon",    "parsing_engine"),
            Leaf("Page Size Settings", "ConvertIcon", "pageSize_settings")
        ));

        Pages.Add(Leaf("Advanced Settings", "ConfigFileIcon", "advanced"));

        // expand first group by default
        if (Pages.Count > 0) Pages[0].IsExpanded = true;
    }

    private static SettingsPageNode Group(string title, string icon, string id,
        params SettingsPageNode[] children)
    {
        var node = new SettingsPageNode { Title = title, IconKey = icon, PageId = id, IsExpanded = false };
        foreach (var c in children) node.Children.Add(c);
        return node;
    }

    private static SettingsPageNode Leaf(string title, string icon, string id) =>
        new() { Title = title, IconKey = icon, PageId = id };
}
