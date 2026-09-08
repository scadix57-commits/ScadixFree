using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace Scadix.Designer;

public partial class SolutionExplorerView : UserControl
{
    // The node that was right-clicked — used by context menu handlers
    private SolutionNode? _contextNode;

    public SolutionExplorerView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
            DataContext = this.FindAncestorOfType<Window>()?.DataContext;
    }

    // ── Tree interaction ─────────────────────────────────────────────────

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TreeView tree && tree.SelectedItem is SolutionNode node)
        {
            if (DataContext is MainWindowViewModel shell)
            {
                shell.SelectedSolutionNode = node;

                if (node.IsOpenable)
                {
                    shell.OpenFile(node.FilePath);
                }
            }
        }
    }

  
    private void ShowContextMenu(SolutionNode node)
    {
        var isSolution = node.Kind == SolutionNodeKind.Solution;
        var isProject = node.Kind == SolutionNodeKind.Project;
        var isProjectRef = node.Kind == SolutionNodeKind.ProjectRef;
        var isFolder = node.Kind is SolutionNodeKind.Folder or SolutionNodeKind.SolutionFolder;
        var isFile = node.Kind == SolutionNodeKind.File;

        var menu = new ContextMenu();

        // ── 1. Add / Manage ──────────────────
        if (isSolution)
        {
            menu.Items.Add(MakeItem("Add Project...", AddProject_Click, "➕"));
            menu.Items.Add(MakeItem("Add Existing Project...", AddExistingProject_Click, "📥"));
            menu.Items.Add(MakeItem("Manage NuGet Packages...", ManageNuGetPackages_Click, "📦"));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Edit Solution File", (s, e) => { if (node.FilePath != null) MainWindowViewModel.Instance.OpenFile(node.FilePath); }, "📝"));
        }
        else if (isProject)
        {
            menu.Items.Add(MakeItem("Add Project Reference...", AddReference_Click, "🔗"));
            menu.Items.Add(MakeItem("Manage NuGet Packages...", ManageNuGetPackages_Click, "📦"));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Edit Project File", (s, e) => { if (node.FilePath != null) MainWindowViewModel.Instance.OpenFile(node.FilePath); }, "📝"));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Create Git Repository...", CreateGitRepo_Click, "➕"));
        }

        if (isSolution || isProject || isFolder)
        {
            menu.Items.Add(MakeItem("Add Folder", AddFolder_Click, "📁"));
            menu.Items.Add(MakeItem("Add File...", AddFile_Click, "📄"));
        }

        // ── 2. Removal ──────────────────────
        if (isProject || isProjectRef || isFile || isFolder)
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            var header = isProject ? "Delete Project" : 
                         isProjectRef ? "Remove Reference" : "Delete";
            menu.Items.Add(MakeItem(header, Delete_Click, "❌"));
        }

        // ── 3. Build / Startup ──────────────
        if (isSolution || isProject)
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());

            if (isProject)
                menu.Items.Add(MakeItem("Set as Startup Project", SetStartup_Click, "🚀"));

            menu.Items.Add(MakeItem("Build", Build_Click, "🔨"));
            menu.Items.Add(MakeItem("Rebuild", Rebuild_Click, "🔄"));
            menu.Items.Add(MakeItem("Clean", Clean_Click, "🧹"));
        }

        // ── 4. File / Explorer ──────────────
        if (node.FilePath != null)
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Open in File Explorer", OpenExplorer_Click, "📂"));
            menu.Items.Add(MakeItem("Copy Full Path", CopyPath_Click, "📋"));
        }

        if (isFile)
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Open", (s, e) => { if (node.FilePath != null) MainWindowViewModel.Instance.OpenFile(node.FilePath); }, "📝"));
        }

        if (menu.Items.Count == 0) return;
        menu.Open(SolutionTree);
    }

    private static MenuItem MakeItem(string header, EventHandler<RoutedEventArgs> handler, string? icon = null)
    {
        var item = new MenuItem { Header = header };
        item.Click += handler;
        if (icon != null)
        {
            item.Icon = new TextBlock { Text = icon, FontSize = 12, FontFamily = "Segoe UI Emoji" };
        }
        return item;
    }

    // ── Add Project ───────────────────────────────────────────────────────

    private void AddProject_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new NewSolutionWindow();
        var welcome = this.FindAncestorOfType<Window>() as WelcomeScreen;
        if (welcome != null) dlg.OwnerWelcomeScreen = welcome;
        dlg.ShowDialog(this.FindAncestorOfType<Window>()!);
    }

    private void AddExistingProject_Click(object? sender, RoutedEventArgs e)
    {
        MainWindowViewModel.Instance.AddExistingProject();
    }

    private void CreateGitRepo_Click(object? sender, RoutedEventArgs e)
    {
        if (_contextNode == null || string.IsNullOrEmpty(_contextNode.FilePath)) return;
        var dir = File.Exists(_contextNode.FilePath) ? Path.GetDirectoryName(_contextNode.FilePath) : _contextNode.FilePath;
        if (!string.IsNullOrEmpty(dir))
        {
            MainWindowViewModel.Instance.Factory.OpenCreateGitRepository(dir);
        }
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (_contextNode != null)
        {
            MainWindowViewModel.Instance.DeleteNode(_contextNode);
        }
    }

    private void CopyPath_Click(object? sender, RoutedEventArgs e)
    {
        if (_contextNode != null)
        {
            MainWindowViewModel.Instance.CopyProjectPath(_contextNode);
        }
    }

    private async void AddReference_Click(object? sender, RoutedEventArgs e)
    {
        if (_contextNode == null || _contextNode.Kind != SolutionNodeKind.Project || string.IsNullOrEmpty(_contextNode.FilePath)) return;

        var owner = this.FindAncestorOfType<Window>();
        if (owner == null) return;

        var dlg = new AddReferenceWindow();
        
        // Get all projects in solution
        var allProjects = GetAllProjects(MainWindowViewModel.Instance.SolutionTree);
        var existingRefs = GetExistingReferences(_contextNode);
        dlg.LoadProjects(allProjects, _contextNode.FilePath, existingRefs);

        if (await dlg.ShowDialog<bool>(owner))
        {
            if (dlg.SelectedProjects.Any())
            {
                foreach (var proj in dlg.SelectedProjects)
                {
                    if (!string.IsNullOrEmpty(proj.FilePath))
                    {
                        SolutionService.AddProjectReference(_contextNode.FilePath, proj.FilePath);
                    }
                }
                MainWindowViewModel.Instance.RefreshSolution();
            }
        }
    }

    private List<string> GetExistingReferences(SolutionNode projectNode)
    {
        var refs = new List<string>();
        var deps = projectNode.Children.FirstOrDefault(c => c.Kind == SolutionNodeKind.DependenciesGroup);
        if (deps != null)
        {
            var projRefs = deps.Children.FirstOrDefault(c => c.Kind == SolutionNodeKind.ProjectRefsGroup);
            if (projRefs != null)
            {
                refs.AddRange(projRefs.Children.Select(c => c.FilePath).Where(p => p != null)!);
            }
        }
        return refs;
    }

    private List<SolutionNode> GetAllProjects(IEnumerable<SolutionNode> nodes)
    {
        var list = new List<SolutionNode>();
        foreach (var node in nodes)
        {
            if (node.Kind == SolutionNodeKind.Project) list.Add(node);
            list.AddRange(GetAllProjects(node.Children));
        }
        return list;
    }

    // ── Add Folder ────────────────────────────────────────────────────────

    private async void AddFolder_Click(object? sender, RoutedEventArgs e)
    {
        var parentDir = GetNodeDirectory(_contextNode);
        if (parentDir == null) return;

        var name = await PromptAsync("New Folder", "Folder name:", "NewFolder");
        if (string.IsNullOrWhiteSpace(name)) return;

        var newDir = Path.Combine(parentDir, name);
        try
        {
            Directory.CreateDirectory(newDir);
            MainWindowViewModel.Instance.RefreshSolution();
        }
        catch (Exception ex)
        {
            MainWindowViewModel.ReportException(ex);
        }
    }

    // ── Add File ──────────────────────────────────────────────────────────

    private async void AddFile_Click(object? sender, RoutedEventArgs e)
    {
        var parentDir = GetNodeDirectory(_contextNode);
        if (parentDir == null) return;

        var owner = this.FindAncestorOfType<Window>();
        if (owner == null) return;

        var dlg = new NewFileWindow(parentDir);
        await dlg.ShowDialog(owner);

        if (dlg.CreatedFilePath != null)
        {
            MainWindowViewModel.Instance.RefreshSolution();
            MainWindowViewModel.Instance.OpenFile(dlg.CreatedFilePath);
        }
    }

    // ── ManageNuGetPackages ──────────────────────────────────────────────────────────

    private void ManageNuGetPackages_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            MainWindowViewModel.Instance.Factory.OpenNuGetManager(_contextNode);
        }
        catch (Exception ex)
        {
            MainWindowViewModel.ReportException(ex);
        }
    }



    private void SetStartup_Click(object? sender, RoutedEventArgs e)
    {
        if (_contextNode != null && DataContext is MainWindowViewModel shell)
        {
            shell.SelectedStartupProject = _contextNode;
        }
    }

    // ── Build / Rebuild / Clean ───────────────────────────────────────────

    private void Build_Click(object? sender, RoutedEventArgs e)
        => RunDotnetCommand("build");

    private void Rebuild_Click(object? sender, RoutedEventArgs e)
        => RunDotnetCommand("build --no-incremental");

    private void Clean_Click(object? sender, RoutedEventArgs e)
        => RunDotnetCommand("clean");

    private void RunDotnetCommand(string command)
    {
        var path = GetProjectOrSolutionPath(_contextNode);
        if (path == null) return;

        _ = MainWindowViewModel.Instance.RunDotnetCommand(command, path);
    }

    // ── Open in File Explorer ─────────────────────────────────────────────

    private void OpenExplorer_Click(object? sender, RoutedEventArgs e)
    {
        var path = _contextNode?.FilePath;
        if (path == null) return;

        var dir = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        if (dir != null && Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"")
            { UseShellExecute = true });
        }
    }

    // ── Toolbar buttons ───────────────────────────────────────────────────

    private void OpenSolution_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel shell) shell.OpenSolutionDialog();
    }

    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel shell) shell.OpenFolderDialog();
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel shell) shell.RefreshSolution();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string? GetNodeDirectory(SolutionNode? node)
    {
        if (node == null) return null;
        if (node.FilePath == null) return null;

        return File.Exists(node.FilePath)
            ? Path.GetDirectoryName(node.FilePath)
            : node.FilePath;
    }

    private static string? GetProjectOrSolutionPath(SolutionNode? node)
    {
        if (node?.FilePath == null) return null;
        return node.FilePath;
    }

    /// <summary>Simple text input dialog using a Window.</summary>
    private async Task<string?> PromptAsync(string title, string label, string defaultValue)
    {
        var owner = this.FindAncestorOfType<Window>();
        if (owner == null) return null;

        var result = defaultValue;
        var tcs = new TaskCompletionSource<string?>();

        var win = new Window
        {
            Title = title,
            Width = 360,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var tb = new TextBox { Text = defaultValue, Margin = new Avalonia.Thickness(12, 8) };
        tb.SelectAll();

        var ok = new Button { Content = "OK", Width = 80, Margin = new Avalonia.Thickness(4) };
        var cancel = new Button { Content = "Cancel", Width = 80, Margin = new Avalonia.Thickness(4) };

        ok.Click += (_, _) => { tcs.TrySetResult(tb.Text); win.Close(); };
        cancel.Click += (_, _) => { tcs.TrySetResult(null); win.Close(); };
        win.Closed += (_, _) => tcs.TrySetResult(null);

        win.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = label },
                tb,
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Children = { ok, cancel }
                }
            }
        };

        await win.ShowDialog(owner);
        return await tcs.Task;
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        var node = GetNodeFromSource(e.Source);
        if (node?.IsOpenable == true && DataContext is MainWindowViewModel shell)
        {
            shell.OpenFile(node.FilePath);
            e.Handled = true;
        }
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Only handle right-click
        if (e.InitialPressMouseButton != MouseButton.Right) return;

        _contextNode = GetNodeFromSource(e.Source);
        if (_contextNode != null)
        {
            ShowContextMenu(_contextNode);
            e.Handled = true;
        }
    }

    private static SolutionNode? GetNodeFromSource(object? source)
    {
        var visual = source as Avalonia.Visual;
        while (visual != null)
        {
            if (visual is Control ctrl && ctrl.DataContext is SolutionNode node)
                return node;
            visual = visual.GetVisualParent();
        }
        return null;
    }
}
