using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Scadix.Designer.ViewModels;
using Scadix.Designer.ViewModels.NuGet;
using Scadix.Designer.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scadix.Designer;

public class MainDockFactory : Factory
{
    private IRootDock? _rootDock;
    private IDocumentDock? _documentDock;

    // Tool ViewModels — kept as fields so we can access them later
    private Tool _toolbox = new ToolboxViewModel();
    public Tool Toolbox 
    { 
        get => _toolbox;
        set => _toolbox = value;
    }
    public OutlineViewModel    Outline    { get; } = new();
    public PropertiesViewModel Properties { get; } = new();
    public ErrorsViewModel     Errors     { get; } = new();
    public ThumbnailViewModel  Thumbnail  { get; } = new();
    public SolutionViewModel   Solution   { get; } = new();
    public GitRepositoriesViewModel    Git        { get; } = new();
    public GitChangesViewModel Commit     { get; }
  
 
    public SymbolsViewModel Symbols { get; } = new();

    // ── New tools ─────────────────────────────────────────────────────────
    public TerminalViewModel          Terminal          { get; } = new();
    public LogicalTreeViewModel       LogicalTree       { get; } = new();
    public FileExplorerViewModel      FileExplorer      { get; } = new();
    public FindAllReferencesViewModel FindAllReferences { get; } = new();
    public PackageManagerConsoleViewModel PackageConsole { get; } = new();

    // ── Debugger tool panels ──────────────────────────────────────────────
    public BreakpointsViewModel    Breakpoints    { get; } = new();
    public CallStackViewModel      CallStack      { get; } = new();
    public LocalsViewModel         Locals         { get; } = new();
    public WatchersViewModel       Watchers       { get; } = new();
    public DebugSettingsViewModel  DebugSettings  { get; } = new();
    public BuildOutputToolViewModel BuildOutput   { get; } = new();
    public DiagnosticToolViewModel Diagnostics    { get; } = new();

    public IDocumentDock? DocumentDock => _documentDock;

    public MainDockFactory()
    {
        Commit       = new GitChangesViewModel(Git);
       
    }

    public override IRootDock CreateLayout()
    {
        // ── Left: Toolbox (top 60%) + Outline (bottom 40%) ──
        var leftPane = new ProportionalDock
        {
            Id = "LeftPane",
            Proportion = 0.20,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                new ToolDock
                {
                    Id = "ToolboxDock",
                    Proportion = 0.60,
                    Alignment = Alignment.Left,
                    ActiveDockable = Toolbox,
                    VisibleDockables = CreateList<IDockable>(Toolbox, Symbols)
                },
                new ProportionalDockSplitter(),
                new ToolDock
                {
                    Id = "OutlineDock",
                    Proportion = 0.40,
                    Alignment = Alignment.Left,
                    ActiveDockable = Outline,
                    VisibleDockables = CreateList<IDockable>(Outline, LogicalTree, FileExplorer)
                })
        };

        // ── Center: Documents (top) + Errors (bottom) ────────────────────
        var documentDock = new DocumentDock
        {
            Id = "Documents",
            Proportion = 0.75,
            IsCollapsable = false,
            CanCreateDocument = false,
            VisibleDockables = CreateList<IDockable>()
        };
        _documentDock = documentDock;

        // When the user clicks a different tab, sync Shell.CurrentDocument.
        ((System.ComponentModel.INotifyPropertyChanged)documentDock).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentDock.ActiveDockable) &&
                documentDock.ActiveDockable is DocumentViewModel vm)
            {
                MainWindowViewModel.Instance.CurrentDocument = vm.Document;
            }
        };

        var centerPane = new ProportionalDock
        {
            Id = "CenterPane",
            Proportion = 0.55,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                _documentDock,
                new ProportionalDockSplitter(),
                new ToolDock
                {
                    Id = "ErrorsDock",
                    Proportion = 0.25,
                    Alignment = Alignment.Bottom,
                    ActiveDockable = Errors,
                    VisibleDockables = CreateList<IDockable>(
                        Errors, Terminal, FindAllReferences, PackageConsole,
                        BuildOutput, Breakpoints, CallStack, Locals, Watchers)
                })
        };

        // ── Right: Solution (top 50%) + Properties/Thumbnail (bottom 50%) ──────────
        var rightPane = new ProportionalDock
        {
            Id = "RightPane",
            Proportion = 0.25,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                new ToolDock
                {
                    Id = "SolutionDock",
                    Proportion = 0.50,
                    Alignment = Alignment.Right,
                    ActiveDockable = Solution,
                    VisibleDockables = CreateList<IDockable>(Solution, Commit)
                },
                new ProportionalDockSplitter(),
                new ToolDock
                {
                    Id = "PropertiesDock",
                    Proportion = 0.50,
                    Alignment = Alignment.Right,
                    ActiveDockable = Properties,
                    VisibleDockables = CreateList<IDockable>(Properties, Thumbnail, Diagnostics, DebugSettings)
                })
        };

        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftPane,
                new ProportionalDockSplitter(),
                centerPane,
                new ProportionalDockSplitter(),
                rightPane)
        };

        _rootDock = CreateRootDock();
        _rootDock.IsCollapsable = false;
        _rootDock.ActiveDockable = mainLayout;
        _rootDock.DefaultDockable = mainLayout;
        _rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);

        return _rootDock;
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            [Toolbox.Id]       = () => Toolbox,
            [Outline.Id]       = () => Outline,
            [Properties.Id]    = () => Properties,
            [Errors.Id]        = () => Errors,
            [Thumbnail.Id]     = () => Thumbnail,
            [Solution.Id]      = () => Solution,
            [Git.Id]           = () => Git,
            [Commit.Id]        = () => Commit,
            
           
            // ── New tools ─────────────────────────────────────────────────
            [Terminal.Id]          = () => Terminal,
            [LogicalTree.Id]       = () => LogicalTree,
            [FileExplorer.Id]      = () => FileExplorer,
            [FindAllReferences.Id] = () => FindAllReferences,
            [PackageConsole.Id]    = () => PackageConsole,
            // ── Debugger ──────────────────────────────────────────────────
            [Breakpoints.Id]   = () => Breakpoints,
            [CallStack.Id]     = () => CallStack,
            [Locals.Id]        = () => Locals,
            [Watchers.Id]      = () => Watchers,
            [DebugSettings.Id] = () => DebugSettings,
            [BuildOutput.Id]   = () => BuildOutput,
            [Diagnostics.Id]   = () => Diagnostics,
        };

        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"]      = () => _rootDock,
            ["Documents"] = () => _documentDock,
        };

        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }

    /// <summary>Add a new document tab for the given Document.</summary>
    public void AddDocument(Document doc)
    {
        if (_documentDock is null || _rootDock is null) return;

        var vm = new DocumentViewModel(doc) { Id = doc.Name, Title = doc.Title };
        AddDockable(_documentDock, vm);
        SetActiveDockable(vm);
        SetFocusedDockable(_documentDock, vm);
    }

    /// <summary>Open a side-by-side diff for a changed file (singleton per file).</summary>
    public void OpenDiff(GitChangeNode node)
    {
        if (_documentDock is null) return;

        var id = $"Diff:{node.FullPath}";

        // إذا كان مفتوحاً بالفعل، فعّله
        var existing = _documentDock.VisibleDockables?
            .OfType<GitDiffDocumentViewModel>()
            .FirstOrDefault(d => d.Id == id);

        if (existing != null)
        {
            SetActiveDockable(existing);
            SetFocusedDockable(_documentDock, existing);
            return;
        }

        var repoPath = GetRepoPath();
        if (string.IsNullOrEmpty(repoPath)) return;

        var docVm = new GitDiffDocumentViewModel(node, repoPath);
        AddDockable(_documentDock, docVm);
        SetActiveDockable(docVm);
        SetFocusedDockable(_documentDock, docVm);
    }

    private string GetRepoPath()
    {
        if (MainWindowViewModel.Instance?.SolutionTree == null || MainWindowViewModel.Instance.SolutionTree.Count == 0)
            return "";

        foreach (var node in MainWindowViewModel.Instance.SolutionTree)
        {
            var path = FindRepoPath(node);
            if (!string.IsNullOrEmpty(path)) return path;
        }
        return "";
    }

    private static string FindRepoPath(SolutionNode node)
    {
        var current = System.IO.File.Exists(node.FilePath)
            ? System.IO.Path.GetDirectoryName(node.FilePath)
            : node.FilePath;

        while (!string.IsNullOrEmpty(current))
        {
            if (System.IO.Directory.Exists(System.IO.Path.Combine(current, ".git")))
                return current;
            var parent = System.IO.Path.GetDirectoryName(current);
            if (parent == current) break;
            current = parent;
        }

        if (node.Children != null)
            foreach (var child in node.Children)
            {
                var r = FindRepoPath(child);
                if (!string.IsNullOrEmpty(r)) return r;
            }

        return "";
    }

    /// <summary>Open Git Repositories (Branches + Log) as a center-dock tab (singleton).</summary>
    public void OpenGitRepositories()
    {
        if (_documentDock is null) return;

        var existing = _documentDock.VisibleDockables?
            .OfType<GitRepositoriesDocumentViewModel>()
            .FirstOrDefault(d => d.Id == "GitRepositories");

        if (existing != null)
        {
            SetActiveDockable(existing);
            SetFocusedDockable(_documentDock, existing);
            return;
        }

        var docVm = new GitRepositoriesDocumentViewModel(Git);
        AddDockable(_documentDock, docVm);
        SetActiveDockable(docVm);
        SetFocusedDockable(_documentDock, docVm);
    }

    /// <summary>Open Git as a center-dock tab (singleton).</summary>
    public void OpenGitManager()
    {
        if (_documentDock is null) return;

        // إذا كان مفتوحاً بالفعل، فعّله فقط
        var existing = _documentDock.VisibleDockables?
            .OfType<GitDocumentViewModel>()
            .FirstOrDefault(d => d.Id == "GitCenter");

        if (existing != null)
        {
            SetActiveDockable(existing);
            SetFocusedDockable(_documentDock, existing);
            return;
        }

        var docVm = new GitDocumentViewModel(Git);
        AddDockable(_documentDock, docVm);
        SetActiveDockable(docVm);
        SetFocusedDockable(_documentDock, docVm);
    }

    /// <summary>Opens the "Create Git Repository" dialog window.</summary>
    public void OpenCreateGitRepository(string localPath)
    {
        var vm = new CreateGitRepositoryViewModel(localPath);
        var win = new Scadix.Designer.Views.Tools.Git.CreateGitRepositoryWindow(vm);
        
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            win.ShowDialog(desktop.MainWindow);
        }
    }

    

    /// <summary>Open NuGet Package Manager as a center-dock tab (singleton).</summary>
    public void OpenNuGetManager(SolutionNode? selectedNode = null)
    {
        if (_documentDock is null) return;

        // If already open, just activate it
        var existing = _documentDock.VisibleDockables?
            .OfType<NuGetDocumentViewModel>()
            .FirstOrDefault(d => d.Id == "NuGetPackageManager");

        if (existing != null)
        {
            // If a specific node is requested, we might want to refresh it even if it's already open
            if (selectedNode != null)
            {
                existing.NuGetViewModel.LoadProjectEntries(selectedNode);
            }

            SetActiveDockable(existing);
            SetFocusedDockable(_documentDock, existing);
            return;
        }

        // Build ViewModel
        NuGetPackageManagerViewModel nugetVm;
        if (selectedNode != null)
        {
            nugetVm = new NuGetPackageManagerViewModel(selectedNode);
        }
        else if (MainWindowViewModel.Instance.SolutionTree.Count > 0)
        {
            nugetVm = new NuGetPackageManagerViewModel(MainWindowViewModel.Instance.SolutionTree[0]);
        }
        else
        {
            nugetVm = new NuGetPackageManagerViewModel();
        }

        var docVm = new NuGetDocumentViewModel(nugetVm);
        docVm.Id    = "NuGetPackageManager";
        docVm.Title = "NuGet Package Manager";
        AddDockable(_documentDock, docVm);
        SetActiveDockable(docVm);
        SetFocusedDockable(_documentDock, docVm);
    }

   
    public void RemoveDocument(Document doc)
    {
        if (_documentDock?.VisibleDockables is null) return;
        var vm = _documentDock.VisibleDockables
            .OfType<DocumentViewModel>()
            .FirstOrDefault(d => d.Document == doc);
        if (vm != null)
            RemoveDockable(vm, true);
    }

    

    /// <summary>
    /// يبحث عن <see cref="IToolDock"/> بمعرّف محدد في التخطيط الحالي.
    /// </summary>
    public IToolDock? FindToolDockById(string dockId)
    {
        if (_rootDock is null) return null;
        return FindDockableById(_rootDock, dockId) as IToolDock;
    }

    /// <summary>
    /// يبحث بشكل تكراري عن <see cref="IDockable"/> بمعرّف محدد.
    /// </summary>
    private static IDockable? FindDockableById(IDockable root, string id)
    {
        if (root.Id == id) return root;

        if (root is IDock dock && dock.VisibleDockables is not null)
        {
            foreach (var child in dock.VisibleDockables)
            {
                var found = FindDockableById(child, id);
                if (found is not null) return found;
            }
        }

        return null;
    }
}
