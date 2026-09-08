using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace Scadix.Designer.ViewModels;

public class GitChange : ObservableObject
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public string Status   { get; set; } = "";
    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// Node in the Changes tree — either a folder or a file.
/// </summary>
public class GitChangeNode : ObservableObject
{
    public string Name       { get; set; } = "";
    public bool   IsFolder   { get; set; }
    public string Status     { get; set; } = "";   // A, M, D, ? — empty for folders
    public string? FullPath  { get; set; }          // full file path for files
    public string? FilePath  { get; set; }          // relative file path from repo root
    public GitChange? Change { get; set; }          // original change for files

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            SetProperty(ref _isSelected, value);
            // propagate to children
            foreach (var child in Children)
                child.IsSelected = value;
            if (Change != null) Change.IsSelected = value;
        }
    }

    public ObservableCollection<GitChangeNode> Children { get; } = new();
}

public partial class GitChangesViewModel : Tool
{
    private readonly GitRepositoriesViewModel _git;
    private CancellationTokenSource? _debounce;

    public ObservableCollection<GitChange>     LocalChanges { get; } = new();
    public ObservableCollection<GitChangeNode> ChangesTree  { get; } = new();
    public ObservableCollection<GitStashEntry> Stashes      => _git.Stashes;
    public ObservableCollection<GitConflictFile> ConflictFiles => _git.ConflictFiles;

    // ── Conflict helpers ──────────────────────────────────────────────────────
    public bool HasConflicts => _git.ConflictFiles.Count > 0;

    // ── Stash ─────────────────────────────────────────────────────────────────
    private GitStashEntry? _selectedStash;
    public GitStashEntry? SelectedStash
    {
        get => _selectedStash;
        set { SetProperty(ref _selectedStash, value); _git.SelectedStash = value; }
    }

    private string _stashMessage = "";
    public string StashMessage
    {
        get => _stashMessage;
        set { SetProperty(ref _stashMessage, value); _git.StashMessage = value; }
    }

    public int StashCount => _git.Stashes.Count;

    // ── Commit ────────────────────────────────────────────────────────────────
    private string _commitMessage = "";
    public string CommitMessage
    {
        get => _commitMessage;
        set => SetProperty(ref _commitMessage, value);
    }
    
    public string GitUserName => GetGitUserName();

    public string CurrentBranch  => _git.CurrentBranch;
    public string RepoName       => GetRepoName();
    public int    CommitsCount   => _git.PendingPushCommits.Count;

    public ObservableCollection<string> Repositories => _git.DiscoveredRepositories;
    public string? SelectedRepositoryPath
    {
        get => _git.SelectedRepositoryPath;
        set { _git.SelectedRepositoryPath = value; OnPropertyChanged(); }
    }

    public ObservableCollection<GitBranch> Branches => _git.LocalBranches;
    public GitBranch? SelectedBranch
    {
        get => _git.SelectedBranch;
        set 
        { 
            if (value != null) _git.CheckoutBranchAsync(value); 
            OnPropertyChanged();
        }
    }

    // ── Toolbar visibility toggles ────────────────────────────────────────────
    private bool _showFetchButton  = true;
    private bool _showPullButton   = true;
    private bool _showPushButton   = true;
    private bool _showSyncButton   = true;

    public bool ShowFetchButton  { get => _showFetchButton;  set => SetProperty(ref _showFetchButton,  value); }
    public bool ShowPullButton   { get => _showPullButton;   set => SetProperty(ref _showPullButton,   value); }
    public bool ShowPushButton   { get => _showPushButton;   set => SetProperty(ref _showPushButton,   value); }
    public bool ShowSyncButton   { get => _showSyncButton;   set => SetProperty(ref _showSyncButton,   value); }

    // ── View all commits → يفتح GitRepositoriesView في المنتصف ──────────
    [RelayCommand]
    public void ViewAllCommits()
    {
        MainWindowViewModel.Instance.Factory.OpenGitRepositories();
        _ = _git.RefreshAsync(CurrentBranch);
    }

    // ── Create Repository ───────────────────────────────────────────────────
    [RelayCommand]
    public void CreateGitRepository()
    {
        var repo = GetRepoPath();
        if (string.IsNullOrEmpty(repo))
        {
            // Fallback to project folder
            repo = _git.GetProjectFolder();
        }
        
        if (!string.IsNullOrEmpty(repo))
        {
            MainWindowViewModel.Instance.Factory.OpenCreateGitRepository(repo);
        }
    }

    // ── Manage Remotes ────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task ManageRemotes()
    {
        var repo = GetRepoPath();
        if (string.IsNullOrEmpty(repo)) return;

        // Load current remotes
        var remotesRaw = await RunGitCommandAsync(repo, "remote -v");
        var remotes = new ObservableCollection<GitRemoteEntry>();
        foreach (var line in remotesRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 2) continue;
            var name = parts[0].Trim();
            var urlAndType = parts[1].Trim();
            // only take "(fetch)" lines to avoid duplicates
            if (!urlAndType.EndsWith("(fetch)")) continue;
            var url = urlAndType.Replace("(fetch)", "").Trim();
            if (!remotes.Any(r => r.Name == name))
                remotes.Add(new GitRemoteEntry { Name = name, Url = url });
        }

        await ShowManageRemotesDialogAsync(repo, remotes);
    }

    private async Task ShowManageRemotesDialogAsync(string repo, ObservableCollection<GitRemoteEntry> remotes)
    {
        var win = new Window
        {
            Title = "Manage Remotes",
            Width = 520, Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };

        var listBox = new ListBox
        {
            ItemsSource = remotes,
            Margin = new Avalonia.Thickness(8, 8, 8, 4),
            Height = 180
        };
        listBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<GitRemoteEntry>((entry, _) =>
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            sp.Children.Add(new TextBlock { Text = entry.Name, FontWeight = FontWeight.SemiBold, Width = 100 });
            sp.Children.Add(new TextBlock { Text = entry.Url, Foreground = Brushes.Gray });
            return sp;
        });

        // Add remote controls
        var nameBox = new TextBox { Watermark = "Name (e.g. origin)", Margin = new Avalonia.Thickness(8, 4, 4, 4), Width = 120 };
        var urlBox  = new TextBox { Watermark = "URL (https://...)",  Margin = new Avalonia.Thickness(4, 4, 4, 4) };
        var addBtn  = new Button  { Content = "Add",    Margin = new Avalonia.Thickness(4, 4, 8, 4), Padding = new Avalonia.Thickness(12, 0) };
        var removeBtn = new Button { Content = "Remove", Margin = new Avalonia.Thickness(4, 4, 8, 4), Padding = new Avalonia.Thickness(12, 0) };
        addBtn.Classes.Add("accent");

        addBtn.Click += async (_, _) =>
        {
            var name = nameBox.Text?.Trim();
            var url  = urlBox.Text?.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) return;
            await RunGitCommandAsync(repo, $"remote add \"{name}\" \"{url}\"");
            remotes.Add(new GitRemoteEntry { Name = name, Url = url });
            nameBox.Text = "";
            urlBox.Text  = "";
        };

        removeBtn.Click += async (_, _) =>
        {
            if (listBox.SelectedItem is not GitRemoteEntry selected) return;
            await RunGitCommandAsync(repo, $"remote remove \"{selected.Name}\"");
            remotes.Remove(selected);
        };

        var addRow = new Grid { Margin = new Avalonia.Thickness(0, 4, 0, 0) };
        addRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        addRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        addRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        addRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Grid.SetColumn(nameBox,    0);
        Grid.SetColumn(urlBox,     1);
        Grid.SetColumn(addBtn,     2);
        Grid.SetColumn(removeBtn,  3);
        addRow.Children.Add(nameBox);
        addRow.Children.Add(urlBox);
        addRow.Children.Add(addBtn);
        addRow.Children.Add(removeBtn);

        var closeBtn = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right,
                                    Margin = new Avalonia.Thickness(8), Padding = new Avalonia.Thickness(16, 0) };
        closeBtn.Click += (_, _) => win.Close();

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(listBox,  0);
        Grid.SetRow(addRow,   1);
        Grid.SetRow(closeBtn, 2);
        root.Children.Add(listBox);
        root.Children.Add(addRow);
        root.Children.Add(closeBtn);

        win.Content = root;
        await ShowDialogAsync(win);
    }

    // ── New Branch ────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task NewBranch()
    {
        var vm  = new NewBranchViewModel(_git);
        var win = new Scadix.Designer.Views.Tools.Git.NewBranchWindow(vm);
        await ShowDialogAsync(win);
        // refresh after dialog closes (branch may have been created)
        await _git.RefreshAsync();
        await RefreshAsync();
    }

    // ── Manage Branches ───────────────────────────────────────────────────────
    [RelayCommand]
    public void ManageBranches()
    {
        // يفتح GitRepositories مع التركيز على تبويب الـ branches
        MainWindowViewModel.Instance.Factory.OpenGitRepositories();
        _git.SelectedTabIndex = 1; // Branches tab
    }

    // ── Open in File Explorer ─────────────────────────────────────────────────
    [RelayCommand]
    public void OpenInFileExplorer()
    {
        var repo = GetRepoPath();
        if (string.IsNullOrEmpty(repo)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName        = repo,
            UseShellExecute = true
        });
    }

    // ── Open in Terminal ──────────────────────────────────────────────────────
    [RelayCommand]
    public void OpenInTerminal()
    {
        var repo = GetRepoPath();
        if (string.IsNullOrEmpty(repo)) return;

        // Windows Terminal → fallback to cmd
        var wtPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WindowsApps\wt.exe");

        if (File.Exists(wtPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName         = wtPath,
                Arguments        = $"-d \"{repo}\"",
                UseShellExecute  = true
            });
        }
        else
        {
            Process.Start(new ProcessStartInfo
            {
                FileName         = "cmd.exe",
                WorkingDirectory = repo,
                UseShellExecute  = true
            });
        }
    }

    // ── Show Toolbar Actions toggles ──────────────────────────────────────────
    [RelayCommand] public void ToggleFetchVisible() => ShowFetchButton = !ShowFetchButton;
    [RelayCommand] public void TogglePullVisible()  => ShowPullButton  = !ShowPullButton;
    [RelayCommand] public void TogglePushVisible()  => ShowPushButton  = !ShowPushButton;
    [RelayCommand] public void ToggleSyncVisible()  => ShowSyncButton  = !ShowSyncButton;

    // ── Helper: show dialog centered on main window ───────────────────────────
    private static Task ShowDialogAsync(Window win)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow != null)
            return win.ShowDialog(desktop.MainWindow);
        win.Show();
        return Task.CompletedTask;
    }

    // ── Git remote operations (delegates to GitRepositoriesViewModel) ─────────
    public Task PullAsync()        => _git.PullAsync();
    public Task PushAsync()        => _git.PushAsync();
    public Task FetchAsync()       => _git.FetchAsync();
    public async Task PullRebaseAsync()
    {
        var repo = _git.GetRepoPath(); if (repo == null) return;
        _git.IsBusy = true;
        await RunGitCommandAsync(repo, "pull --rebase");
        await _git.RefreshAsync();
        await RefreshAsync();
    }

    // ── Open diff for a file node ─────────────────────────────────────────
    [RelayCommand]
    public void OpenFileDiff(GitChangeNode? node)
    {
        if (node == null || node.IsFolder) return;
        MainWindowViewModel.Instance.Factory.OpenDiff(node);
    }
    

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public void Clear()
    {
        LocalChanges.Clear();
        ChangesTree.Clear();
        CommitMessage = "";
        SelectedStash = null;
        StashMessage = "";
    }

    public GitChangesViewModel(GitRepositoriesViewModel git)
    {
        _git = git;
        Id = "GitChanges";
        Title = "Git Changes";
        CanClose = true;

        Dispatcher.UIThread.Post(Initialize);
    }

    private void Initialize()
    {
        // 1. Watch for project open/close
        MainWindowViewModel.Instance.SolutionTree.CollectionChanged += (_, _) => ScheduleRefresh();

        // 2. Watch for document saves
        MainWindowViewModel.Instance.Documents.CollectionChanged += OnDocumentsChanged;
        foreach (var doc in MainWindowViewModel.Instance.Documents)
            doc.PropertyChanged += OnDocumentPropertyChanged;

        // 3. Forward conflict/stash changes from GitToolViewModel
        _git.ConflictFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasConflicts));
            OnPropertyChanged(nameof(ConflictFiles));
        };
        _git.Stashes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(StashCount));
        _git.PendingPushCommits.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CommitsCount));

        // 4. Listen for branch changes in the repository view model
        _git.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(GitRepositoriesViewModel.CurrentBranch))
                OnPropertyChanged(nameof(CurrentBranch));
            if (e.PropertyName == nameof(GitRepositoriesViewModel.SelectedRepositoryPath))
            {
                OnPropertyChanged(nameof(SelectedRepositoryPath));
                OnPropertyChanged(nameof(RepoName));
                ScheduleRefresh();
            }
        };

        // 5. Watch for active document change to switch repo
        MainWindowViewModel.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.CurrentDocument))
                UpdateRepoFromActiveDocument();
        };

        // 6. Initial refresh
        UpdateRepoFromActiveDocument();
        ScheduleRefresh();
    }

    private void UpdateRepoFromActiveDocument()
    {
        var activeDoc = MainWindowViewModel.Instance.CurrentDocument;
        if (activeDoc == null || string.IsNullOrEmpty(activeDoc.FilePath)) return;

        var path = activeDoc.FilePath;
        var dir = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                if (Repositories.Contains(dir))
                {
                    SelectedRepositoryPath = dir;
                }
                break;
            }
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (Document doc in e.NewItems)
                doc.PropertyChanged += OnDocumentPropertyChanged;

        if (e.OldItems != null)
            foreach (Document doc in e.OldItems)
                doc.PropertyChanged -= OnDocumentPropertyChanged;
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Refresh when a document is saved (IsDirty becomes false)
        if (e.PropertyName == nameof(Document.IsDirty))
            ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        // Debounce 600ms to avoid rapid-fire refreshes
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        Task.Delay(600, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
                Dispatcher.UIThread.Post(() => _ = RefreshAsync());
        }, TaskScheduler.Default);
    }

    public async Task RefreshAsync()
    {
        var repoPath = GetRepoPath();
        if (string.IsNullOrEmpty(repoPath)) return;

        IsBusy = true;
        try
        {
            Title = $"Git Changes - {RepoName}";
            var statusRaw = await RunGitCommandAsync(repoPath, "status --porcelain");
            Dispatcher.UIThread.Post(() =>
            {
                LocalChanges.Clear();
                foreach (var line in statusRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Length > 3)
                        LocalChanges.Add(new GitChange
                        {
                            Status   = line.Substring(0, 2).Trim(),
                            FilePath = line.Substring(3).Trim()
                        });
                }

                // بناء شجرة المجلدات
                BuildChangesTree(repoPath);
            });
        }
        catch { }
        finally { IsBusy = false; }
    }

    private void BuildChangesTree(string repoPath)
    {
        ChangesTree.Clear();

        // Root node = repo folder name
        var root = new GitChangeNode
        {
            Name     = Path.GetFileName(repoPath),
            IsFolder = true,
            FullPath = repoPath
        };

        foreach (var change in LocalChanges)
        {
            // تحويل المسار النسبي إلى أجزاء
            var parts = change.FilePath.Replace('\\', '/').Split('/');
            var current = root;

            for (int i = 0; i < parts.Length; i++)
            {
                var part  = parts[i];
                bool last = i == parts.Length - 1;

                if (last)
                {
                    // ملف
                    current.Children.Add(new GitChangeNode
                    {
                        Name     = part,
                        IsFolder = false,
                        Status   = change.Status,
                        FullPath = Path.Combine(repoPath, change.FilePath),
                        FilePath = change.FilePath,
                        Change   = change
                    });
                }
                else
                {
                    // مجلد — ابحث أو أنشئ
                    var folder = current.Children
                        .FirstOrDefault(n => n.IsFolder && n.Name == part);
                    if (folder == null)
                    {
                        folder = new GitChangeNode
                        {
                            Name     = part,
                            IsFolder = true,
                            FullPath = Path.Combine(repoPath, string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(i + 1)))
                        };
                        current.Children.Add(folder);
                    }
                    current = folder;
                }
            }
        }

        // أضف الـ root فقط إذا كان له أبناء
        if (root.Children.Count > 0)
            ChangesTree.Add(root);
    }

    public async Task CommitAsync()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage)) return;
        var repoPath = GetRepoPath();
        if (string.IsNullOrEmpty(repoPath)) return;

        IsBusy = true;
        try
        {
            var selectedFiles = LocalChanges.Where(c => c.IsSelected).Select(c => c.FilePath).ToList();
            if (selectedFiles.Count == 0)
                await RunGitCommandAsync(repoPath, "add .");
            else
                foreach (var file in selectedFiles)
                    await RunGitCommandAsync(repoPath, $"add \"{file}\"");

            await RunGitCommandAsync(repoPath, $"commit -m \"{CommitMessage.Replace("\"", "\\\"")}\"");
            CommitMessage = "";
            _ = RefreshAsync();
            _ = _git.RefreshAsync();
        }
        catch { }
        finally { IsBusy = false; }
    }

    private string? GetRepoPath()
    {
        return _git.GetRepoPath();
    }

    private static void CollectPaths(SolutionNode node, List<string> paths)
    {
        if (!string.IsNullOrEmpty(node.FilePath))
            paths.Add(node.FilePath);
        if (node.Children != null)
            foreach (var child in node.Children)
                CollectPaths(child, paths);
    }

    private async Task<string> RunGitCommandAsync(string workingDir, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null) return "";

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    // ── Stash delegates ───────────────────────────────────────────────────────
    public Task StashAsync()      => _git.StashAsync();
    public Task StashPopAsync()   => _git.StashPopAsync();
    public Task StashApplyAsync() => _git.StashApplyAsync();
    public Task StashDropAsync()  => _git.StashDropAsync();

    // ── Conflict delegates ────────────────────────────────────────────────────
    public Task NavigateToConflictsAsync()
    {
        // Switch Git tool to Conflicts tab (index 3)
        _git.SelectedTabIndex = 3;
        return Task.CompletedTask;
    }

    // ── Rollback / Diff ───────────────────────────────────────────────────────
    public async Task RollbackAsync(GitChange change)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitCommandAsync(repo, $"checkout -- \"{change.FilePath}\"");
        await RefreshAsync();
    }

    public async Task RollbackAllAsync()
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitCommandAsync(repo, "checkout -- .");
        await RefreshAsync();
    }

    public Task ShowDiffAsync(GitChange change)
    {
        // TODO: open diff viewer
        return Task.CompletedTask;
    }

    // ── Commit and Push ───────────────────────────────────────────────────────
    public async Task CommitAndPushAsync()
    {
        await CommitAsync();
        await _git.PreparePushAsync();
    }

    // ── Select All ────────────────────────────────────────────────────────────
    public Task SelectAllAsync()
    {
        foreach (var c in LocalChanges) c.IsSelected = true;
        return Task.CompletedTask;
    }

    // ── Git user name ─────────────────────────────────────────────────────────
    private static string GetGitUserName()
    {
        try
        {
            var si = new ProcessStartInfo("git", "config user.name")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using var p = Process.Start(si);
            return p?.StandardOutput.ReadToEnd().Trim() ?? "Git User";
        }
        catch { return "Git User"; }
    }

    private string GetRepoName()
    {
        var repo = GetRepoPath();
        return repo != null ? System.IO.Path.GetFileName(repo) : "Repository";
    }
}

// ── Helper model ──────────────────────────────────────────────────────────────
public class GitRemoteEntry : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string Name { get; set; } = "";
    public string Url  { get; set; } = "";
}
