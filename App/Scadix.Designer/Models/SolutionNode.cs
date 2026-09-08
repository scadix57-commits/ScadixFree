using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Scadix.Designer;

/// <summary>Node kinds — used to pick the right icon and behavior.</summary>
public enum SolutionNodeKind
{
    Solution,
    SolutionFolder,
    Project,
    DependenciesGroup,   // "Dependencies" virtual folder
    AnalyzersGroup,      // "Analyzers"
    FrameworksGroup,     // "Frameworks"
    PackagesGroup,       // "Packages"
    ProjectRefsGroup,    // "Projects"
    Package,             // NuGet package
    ProjectRef,          // Project reference
    Folder,
    File,
    Unknown
}

/// <summary>
/// A single node in the Solution Explorer tree.
/// Mirrors the Visual Studio Solution Explorer layout:
///
///   Solution 'XAMLStudio' (N projects)
///   └─ App
///      └─ Scadix.Designer
///         ├─ Dependencies
///         │  ├─ Analyzers
///         │  ├─ Frameworks
///         │  ├─ Packages
///         │  │  ├─ Avalonia (11.3.13)
///         │  │  └─ ...
///         │  └─ Projects
///         │     ├─ Scadix.AxamlDesign
///         │     └─ ...
///         ├─ Converters/
///         ├─ Models/
///         └─ Program.cs
/// </summary>
public class SolutionNode : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private SolutionNodeKind kind = SolutionNodeKind.Unknown;
    public SolutionNodeKind Kind { get => kind; set => SetProperty(ref kind, value); }

    private string name = "";
    public string Name { get => name; set => SetProperty(ref name, value); }

    private string badge = "";
    public string Badge { get => badge; set => SetProperty(ref badge, value); }

    private string? filePath;
    public string? FilePath { get => filePath; set => SetProperty(ref filePath, value); }

    private string? targetFramework;
    public string? TargetFramework { get => targetFramework; set => SetProperty(ref targetFramework, value); }

    private bool isStartup;
    public bool IsStartup 
    { 
        get => isStartup; 
        set 
        {
            if (SetProperty(ref isStartup, value))
            {
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(IconKey));
            }
        }
    }

    private bool isMissing;
    public bool IsMissing
    {
        get => isMissing;
        set
        {
            if (SetProperty(ref isMissing, value))
            {
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(IconKey));
            }
        }
    }

    private bool hasWarning;
    public bool HasWarning
    {
        get => hasWarning;
        set => SetProperty(ref hasWarning, value);
    }

    private string? tooltip;
    public string? Tooltip { get => tooltip; set => SetProperty(ref tooltip, value); }

    // ── Computed helpers ─────────────────────────────────────────────────

    public bool IsOpenable =>
        FilePath != null &&
        System.IO.File.Exists(FilePath) &&
        Kind == SolutionNodeKind.File;

    public bool HasBadge => !string.IsNullOrEmpty(Badge);

    // ── Icon (VS-style emoji approximation) ──────────────────────────────
    public string Icon => Kind switch
    {
        _ when IsMissing                  => "⚠", 
        _ when IsStartup                  => "▶", // Green arrow for startup project
        SolutionNodeKind.Solution         => "🗂",
        SolutionNodeKind.SolutionFolder   => "📁",
        SolutionNodeKind.Project          => "🔷",
        SolutionNodeKind.DependenciesGroup => "🔗",
        SolutionNodeKind.AnalyzersGroup   => "🔍",
        SolutionNodeKind.FrameworksGroup  => "🖥",
        SolutionNodeKind.PackagesGroup    => "📦",
        SolutionNodeKind.ProjectRefsGroup => "🔷",
        SolutionNodeKind.Package          => "📦",
        SolutionNodeKind.ProjectRef       => "🔷",
        SolutionNodeKind.Folder           => "📁",
        SolutionNodeKind.File             => FileIcon(FilePath),
        _                                 => "📄"
    };

    /// <summary>Resource key for the DrawingImage defined in Icons.axaml.</summary>
    public string IconKey => Kind switch
    {
        _ when IsMissing                   => "WarningIcon",
        _ when IsStartup                   => "RunIcon",
        SolutionNodeKind.Solution          => "SolutionIcon",
        SolutionNodeKind.SolutionFolder    => "FolderOpenIcon",
        SolutionNodeKind.Project           => "ProjectIcon",
        SolutionNodeKind.DependenciesGroup => "DependenciesIcon",
        SolutionNodeKind.AnalyzersGroup    => "AnalyzerIcon",
        SolutionNodeKind.FrameworksGroup   => "FrameworkIcon",
        SolutionNodeKind.PackagesGroup     => "PackageIcon",
        SolutionNodeKind.ProjectRefsGroup  => "ReferenceIcon",
        SolutionNodeKind.Package           => "PackageIcon",
        SolutionNodeKind.ProjectRef        => "ProjectIcon",
        SolutionNodeKind.Folder            => "FolderIcon",
        SolutionNodeKind.File              => FileIconKey(FilePath),
        _                                  => "TextFileIcon"
    };

    private string FileIcon(string? path)
    {
        if (path is null) return "📄";
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        
        if (ext is ".cs" or ".vb")
        {
            if (Children.Any(c => c.Name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) || 
                                 c.Name.EndsWith(".Designer.vb", StringComparison.OrdinalIgnoreCase) ||
                                 c.Name.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)))
            {
                return "🖥"; // Window emoji for Forms
            }
            return "📝";
        }

        return ext switch
        {
            ".axaml" or ".xaml" => "📄",
            ".fs"               => "📝",
            ".csproj" or ".fsproj" or ".vbproj" => "⚙",
            ".sln" or ".slnx"   => "⊞",
            ".json"             => "{}",
            ".xml"              => "📋",
            ".md"               => "📋",
            ".txt"              => "📋",
            ".png" or ".jpg" or ".ico" => "🖼",
            ".config"           => "⚙",
            ".manifest"         => "⚙",
            _                   => "📄"
        };
    }

    private string FileIconKey(string? path)
    {
        if (path is null) return "TextFileIcon";
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        
        if (ext is ".cs" or ".vb")
        {
            // If this node has children (like Designer.cs or resx), it's likely a Form/UserControl
            if (Children.Any(c => c.Name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) || 
                                 c.Name.EndsWith(".Designer.vb", StringComparison.OrdinalIgnoreCase) ||
                                 c.Name.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)))
            {
                return "WinFormsFileIcon";
            }
            return "CSharpFileIcon";
        }

        return ext switch
        {
            ".axaml" or ".xaml"                   => "XamlFileIcon",
            ".csproj" or ".fsproj" or ".vbproj"   => "ProjectIcon",
            ".sln" or ".slnx"                     => "SolutionIcon",
            ".json"                               => "JsonFileIcon",
            ".xml"                                => "XmlFileIcon",
            ".md" or ".txt"                       => "TextFileIcon",
            ".png" or ".jpg" or ".jpeg" or ".ico" or ".bmp" => "ImageFileIcon",
            ".config" or ".manifest"              => "ConfigFileIcon",
            _                                     => "TextFileIcon"
        };
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value)
                OnExpanded?.Invoke(this);
        }
    }

    public Action<SolutionNode>? OnExpanded { get; set; }

    private bool _isLoaded = false;
    public bool IsLoaded { get => _isLoaded; set => SetProperty(ref _isLoaded, value); }

    public SolutionNode? Parent { get; set; }
    public ObservableCollection<SolutionNode> Children { get; set; } = new();
}
