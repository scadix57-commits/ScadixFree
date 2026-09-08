using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace Scadix.Designer.ViewModels;

// ── Models ────────────────────────────────────────────────────────────────────

public class GitCommit : ObservableObject
{
    public string Hash { get; set; } = "";
    public string Message { get; set; } = "";
    public string Author { get; set; } = "";
    public string AuthorEmail { get; set; } = "";
    public DateTime Date { get; set; }
    public List<string> Parents { get; set; } = new();
    public List<string> RefNames { get; set; } = new();
    public string ShortHash => Hash.Length > 7 ? Hash[..7] : Hash;

    // Graph layout — set by GraphLayoutEngine
    public int GraphColumn { get; set; }
    public List<GraphEdge> GraphEdges { get; set; } = new();

    // Avatar — loaded asynchronously from Gravatar
    private Bitmap? _avatar;
    public Bitmap? Avatar
    {
        get => _avatar;
        set => SetProperty(ref _avatar, value);
    }
}

public class GraphEdge
{
    public int FromCol { get; set; }
    public int ToCol { get; set; }
    public int Color { get; set; } // index into color palette
}

public class GitBranch : ObservableObject
{
    public string Name { get; set; } = "";
    public bool IsRemote { get; set; }
    public bool IsCurrent { get; set; }
}

public class GitTag : ObservableObject
{
    public string Name { get; set; } = "";
    public string Hash { get; set; } = "";
}

public class GitBranchNode : ObservableObject
{
    public string Name { get; set; } = "";
    public bool IsFolder { get; set; }
    public bool IsCurrent { get; set; }
    public GitBranch? Branch { get; set; }
    public ObservableCollection<GitBranchNode> Children { get; } = new();

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

public class GitFileChange : ObservableObject
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public string Status { get; set; } = "";
}

public class GitStashEntry : ObservableObject
{
    public string Index { get; set; } = "";   // stash@{0}
    public string Message { get; set; } = "";
    public string Branch { get; set; } = "";
}

public class GitFileHistoryEntry : ObservableObject
{
    public string Hash { get; set; } = "";
    public string Message { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime Date { get; set; }
    public string ShortHash => Hash.Length > 7 ? Hash[..7] : Hash;
}

public class GitConflictFile : ObservableObject
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);
}

// ── Gravatar Service ──────────────────────────────────────────────────────────

public static class GravatarService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    // Cache: email → Bitmap (null means "no image / 404")
    private static readonly ConcurrentDictionary<string, Bitmap?> _cache = new();

    /// <summary>
    /// Loads the Gravatar for the given email asynchronously.
    /// Returns null if the email has no Gravatar (falls back to initials avatar in UI).
    /// </summary>
    public static async Task<Bitmap?> LoadAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var key = email.Trim().ToLowerInvariant();
        if (_cache.TryGetValue(key, out var cached)) return cached;

        try
        {
            var hash = ComputeMd5(key);
            // d=404 → return HTTP 404 instead of a default image when no Gravatar exists
            // s=64 → 64px for crisp display at both list (18px) and details (36px) sizes
            var url = $"https://www.gravatar.com/avatar/{hash}?s=64&d=404";
            var bytes = await _http.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            var bmp = new Bitmap(ms);
            _cache[key] = bmp;
            return bmp;
        }
        catch
        {
            // 404 or network error → cache null so we don't retry
            _cache[key] = null;
            return null;
        }
    }

    private static string ComputeMd5(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

public class GitRepositoriesViewModel : Tool
{
    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<GitCommit> Commits { get; } = new();
    public ObservableCollection<GitBranch> LocalBranches { get; } = new();
    public ObservableCollection<GitBranch> RemoteBranches { get; } = new();
    public ObservableCollection<GitBranchNode> LocalBranchNodes { get; } = new();
    public ObservableCollection<GitBranchNode> RemoteBranchNodes { get; } = new();
    public ObservableCollection<GitTag> Tags { get; } = new();
    public ObservableCollection<GitFileChange> SelectedCommitFiles { get; } = new();
    public ObservableCollection<GitChangeNode> CommitFilesTree { get; } = new();
    public ObservableCollection<GitChange> LocalChanges { get; } = new();
    public ObservableCollection<string> ConsoleOutput { get; } = new();
    public ObservableCollection<GitStashEntry> Stashes { get; } = new();
    public ObservableCollection<GitFileHistoryEntry> FileHistory { get; } = new();
    public ObservableCollection<GitConflictFile> ConflictFiles { get; } = new();
    public ObservableCollection<GitCommit> PendingPushCommits { get; } = new();
    public ObservableCollection<string> DiscoveredRepositories { get; } = new();

    private string? _selectedRepositoryPath;
    public string? SelectedRepositoryPath
    {
        get => _selectedRepositoryPath;
        set
        {
            if (SetProperty(ref _selectedRepositoryPath, value))
            {
                _ = RefreshAsync();
            }
        }
    }

    // ── Tab ──────────────────────────────────────────────────────────────────
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    // ── Log ──────────────────────────────────────────────────────────────────
    private GitCommit? _selectedCommit;
    public GitCommit? SelectedCommit
    {
        get => _selectedCommit;
        set { if (SetProperty(ref _selectedCommit, value)) _ = RefreshSelectedCommitDetailsAsync(); }
    }

    private string _currentBranch = "";
    public string CurrentBranch
    {
        get => _currentBranch;
        set { if (SetProperty(ref _currentBranch, value)) MainWindowViewModel.Instance.CurrentGitBranch = value; }
    }

    // ── Branches ─────────────────────────────────────────────────────────────
    private GitBranch? _selectedBranch;
    public GitBranch? SelectedBranch
    {
        get => _selectedBranch;
        set => SetProperty(ref _selectedBranch, value);
    }

    private string _newBranchName = "";
    public string NewBranchName
    {
        get => _newBranchName;
        set => SetProperty(ref _newBranchName, value);
    }

    // ── Stash ─────────────────────────────────────────────────────────────────
    private GitStashEntry? _selectedStash;
    public GitStashEntry? SelectedStash
    {
        get => _selectedStash;
        set => SetProperty(ref _selectedStash, value);
    }

    private string _stashMessage = "";
    public string StashMessage
    {
        get => _stashMessage;
        set => SetProperty(ref _stashMessage, value);
    }

    // ── History ───────────────────────────────────────────────────────────────
    private string _historyFilePath = "";
    public string HistoryFilePath
    {
        get => _historyFilePath;
        set => SetProperty(ref _historyFilePath, value);
    }

    private GitFileHistoryEntry? _selectedHistoryEntry;
    public GitFileHistoryEntry? SelectedHistoryEntry
    {
        get => _selectedHistoryEntry;
        set => SetProperty(ref _selectedHistoryEntry, value);
    }

    private string _blameOutput = "";
    public string BlameOutput
    {
        get => _blameOutput;
        set => SetProperty(ref _blameOutput, value);
    }

    // ── Push dialog ───────────────────────────────────────────────────────────
    private bool _showPushDialog;
    public bool ShowPushDialog
    {
        get => _showPushDialog;
        set => SetProperty(ref _showPushDialog, value);
    }

    // ── Busy ─────────────────────────────────────────────────────────────────
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private int _maxGraphColumns = 1;
    public int MaxGraphColumns
    {
        get => _maxGraphColumns;
        set => SetProperty(ref _maxGraphColumns, value);
    }

    // ── Filter ────────────────────────────────────────────────────────────────
    private string _filterText = "";
    public string FilterText
    {
        get => _filterText;
        set { if (SetProperty(ref _filterText, value)) ApplyFilter(); }
    }

    private string _filterAuthor = "";
    public string FilterAuthor
    {
        get => _filterAuthor;
        set { if (SetProperty(ref _filterAuthor, value)) ApplyFilter(); }
    }

    public ObservableCollection<GitCommit> FilteredCommits { get; } = new();

    private void ApplyFilter()
    {
        FilteredCommits.Clear();
        var q = FilterText.ToLowerInvariant();
        var a = FilterAuthor.ToLowerInvariant();
        foreach (var c in Commits)
        {
            if (!string.IsNullOrEmpty(q) &&
                !c.Message.ToLowerInvariant().Contains(q) &&
                !c.ShortHash.ToLowerInvariant().Contains(q))
                continue;
            if (!string.IsNullOrEmpty(a) && !c.Author.ToLowerInvariant().Contains(a))
                continue;
            FilteredCommits.Add(c);
        }
        if (FilteredCommits.Count == 0 && string.IsNullOrEmpty(q) && string.IsNullOrEmpty(a))
            foreach (var c in Commits) FilteredCommits.Add(c);
    }

    // ── Branches collapse ─────────────────────────────────────────────────────
    private bool _localExpanded = true;
    public bool LocalExpanded
    {
        get => _localExpanded;
        set => SetProperty(ref _localExpanded, value);
    }

    private bool _remoteExpanded = true;
    public bool RemoteExpanded
    {
        get => _remoteExpanded;
        set => SetProperty(ref _remoteExpanded, value);
    }

    private bool _tagsExpanded = true;
    public bool TagsExpanded
    {
        get => _tagsExpanded;
        set => SetProperty(ref _tagsExpanded, value);
    }

    private string _branchSearchText = "";
    public string BranchSearchText
    {
        get => _branchSearchText;
        set => SetProperty(ref _branchSearchText, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public void Clear()
    {
        Commits.Clear();
        LocalBranches.Clear();
        RemoteBranches.Clear();
        LocalBranchNodes.Clear();
        RemoteBranchNodes.Clear();
        Tags.Clear();
        Stashes.Clear();
        ConflictFiles.Clear();
        PendingPushCommits.Clear();
        SelectedCommit = null;
        CurrentBranch = "";
        StatusMessage = "No repository open";
    }

    private GitBranchNode? _selectedBranchNode;
    public GitBranchNode? SelectedBranchNode
    {
        get => _selectedBranchNode;
        set
        {
            if (SetProperty(ref _selectedBranchNode, value))
            {
                if (value != null && !value.IsFolder && value.Branch != null)
                {
                    CurrentBranch = value.Branch.Name;
                    _ = RefreshAsync(value.Branch.Name);
                }
            }
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────────
    public GitRepositoriesViewModel()
    {
        Id = "Git";
        Title = "Git";
        CanClose = true;

        Dispatcher.UIThread.Post(Initialize);
    }

    private void Initialize()
    {
        MainWindowViewModel.Instance.SolutionTree.CollectionChanged += (_, _) => TryRefresh();
        TryRefresh();
    }

    private void TryRefresh()
    {
        DiscoverRepositories();
        if (SelectedRepositoryPath != null) { _ = RefreshAsync(); return; }
        
        var repo = GetRepoPath();
        if (repo != null) 
        { 
            SelectedRepositoryPath = repo;
            return; 
        }

        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        t.Tick += (_, _) => { 
            DiscoverRepositories();
            var r = GetRepoPath();
            if (r == null) return; 
            t.Stop(); 
            SelectedRepositoryPath = r;
        };
        t.Start();
    }

    private void DiscoverRepositories()
    {
        if (MainWindowViewModel.Instance?.SolutionTree == null) return;
        
        var candidates = new List<string>();
        foreach (var node in MainWindowViewModel.Instance.SolutionTree) CollectPaths(node, candidates);
        
        var repos = new HashSet<string>();
        foreach (var path in candidates)
        {
            var cur = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            while (!string.IsNullOrEmpty(cur))
            {
                if (Directory.Exists(Path.Combine(cur, ".git")))
                {
                    repos.Add(cur);
                    break;
                }
                var parent = Path.GetDirectoryName(cur);
                if (parent == cur) break;
                cur = parent;
            }
        }

        Dispatcher.UIThread.Post(() =>
        {
            var currentRepos = DiscoveredRepositories.ToList();
            foreach (var r in repos)
            {
                if (!currentRepos.Contains(r)) DiscoveredRepositories.Add(r);
            }
            foreach (var r in currentRepos)
            {
                if (!repos.Contains(r)) DiscoveredRepositories.Remove(r);
            }
        });
    }

    private static async Task LoadAvatarAsync(GitCommit commit)
    {
        var bmp = await GravatarService.LoadAsync(commit.AuthorEmail);
        if (bmp != null)
            await Dispatcher.UIThread.InvokeAsync(() => commit.Avatar = bmp);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────
    public async Task RefreshAsync(string? branchName = null)
    {
        var repo = GetRepoPath();
        if (repo == null) return;
        IsBusy = true;
        try
        {
            var branch = await RunGitAsync(repo, "rev-parse --abbrev-ref HEAD");

            // If branchName is null, show all branches. Otherwise show only that branch.
            string logRange = string.IsNullOrEmpty(branchName)
                ? "--branches --remotes --tags"
                : branchName;

            var commitsRaw = await RunGitAsync(repo, $"log --pretty=format:%H|%P|%s|%an|%ae|%ai|%D {logRange} -n 200");
            var branchesRaw = await RunGitAsync(repo, "branch -a --format=%(refname:short)|%(HEAD)");
            var stashRaw = await RunGitAsync(repo, "stash list --pretty=format:%gd|%s|%gs");
            var conflictRaw = await RunGitAsync(repo, "diff --name-only --diff-filter=U");
            var tagsRaw = await RunGitAsync(repo, "tag --sort=-version:refname --format=%(refname:short)|%(objectname:short)");

            Dispatcher.UIThread.Post(() =>
            {
                CurrentBranch = branch.Trim();
                SelectedCommit = null;

                // Commits
                Commits.Clear();
                foreach (var line in commitsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = line.Split('|');
                    if (p.Length >= 6)
                    {
                        // format: %H|%P|%s|%an|%ae|%ai|%D
                        var parents = p[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                        var refs = p.Length > 6 && !string.IsNullOrWhiteSpace(p[6])
                            ? p[6].Split(',').Select(r => r.Trim()).ToList()
                            : new List<string>();
                        var commit = new GitCommit
                        {
                            Hash = p[0],
                            Parents = parents,
                            Message = p[2],
                            Author = p[3],
                            AuthorEmail = p[4].Trim(),
                            Date = DateTime.TryParse(p[5], out var d) ? d : DateTime.MinValue,
                            RefNames = refs
                        };
                        Commits.Add(commit);
                        // Load avatar in background — deduplicated by GravatarService cache
                        _ = LoadAvatarAsync(commit);
                    }
                }
                GraphLayoutEngine.Layout(Commits);
                MaxGraphColumns = Commits.Count > 0
                    ? Commits.Max(c => c.GraphColumn) + 1
                    : 1;
                ApplyFilter();
                OnPropertyChanged(nameof(Commits));

                // Branches
                LocalBranches.Clear(); RemoteBranches.Clear();
                LocalBranchNodes.Clear(); RemoteBranchNodes.Clear();
                foreach (var line in branchesRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = line.Split('|');
                    var name = p[0].Trim();
                    var isCurrent = p.Length > 1 && p[1].Trim() == "*";
                    if (name.StartsWith("origin/") || name.StartsWith("remotes/"))
                        RemoteBranches.Add(new GitBranch { Name = name, IsRemote = true });
                    else
                        LocalBranches.Add(new GitBranch { Name = name, IsRemote = false, IsCurrent = isCurrent });
                }

                BuildBranchTree(LocalBranches, LocalBranchNodes);
                BuildBranchTree(RemoteBranches, RemoteBranchNodes);

                // Stashes
                Stashes.Clear();
                foreach (var line in stashRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = line.Split('|');
                    Stashes.Add(new GitStashEntry
                    {
                        Index = p.Length > 0 ? p[0] : "",
                        Message = p.Length > 1 ? p[1] : "",
                        Branch = p.Length > 2 ? p[2] : ""
                    });
                }

                // Conflicts
                ConflictFiles.Clear();
                foreach (var f in conflictRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    ConflictFiles.Add(new GitConflictFile { FilePath = f.Trim() });

                // Tags
                Tags.Clear();
                foreach (var line in tagsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = line.Split('|');
                    Tags.Add(new GitTag
                    {
                        Name = p[0].Trim(),
                        Hash = p.Length > 1 ? p[1].Trim() : ""
                    });
                }

                // Unpushed commits for the current branch
                _ = UpdateUnpushedCommitsAsync(repo);

                StatusMessage = "";
            });
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task UpdateUnpushedCommitsAsync(string repo)
    {
        try
        {
            // Try to get commits between upstream and HEAD
            var raw = await RunGitAsync(repo, "log @{u}..HEAD --pretty=format:%H|%s|%an|%ai");
            Dispatcher.UIThread.Post(() =>
            {
                PendingPushCommits.Clear();
                foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = line.Split('|');
                    if (p.Length >= 4)
                        PendingPushCommits.Add(new GitCommit
                        {
                            Hash = p[0],
                            Message = p[1],
                            Author = p[2],
                            Date = DateTime.TryParse(p[3], out var d) ? d : DateTime.MinValue
                        });
                }
                OnPropertyChanged(nameof(PendingPushCommits));
            });
        }
        catch { /* Upstream might not exist */ }
    }

    private void BuildBranchTree(IEnumerable<GitBranch> branches, ObservableCollection<GitBranchNode> rootNodes)
    {
        foreach (var branch in branches)
        {
            var parts = branch.Name.Split('/');
            if (branch.IsRemote && parts.Length > 1 && (parts[0] == "origin" || parts[0] == "remotes"))
            {
                var skip = parts[0] == "remotes" ? 2 : 1;
                if (parts.Length > skip) parts = parts.Skip(skip).ToArray();
            }

            ObservableCollection<GitBranchNode> currentLevel = rootNodes;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                bool isLast = i == parts.Length - 1;

                var existingNode = currentLevel.FirstOrDefault(n => n.Name == part && n.IsFolder == !isLast);
                if (existingNode == null)
                {
                    existingNode = new GitBranchNode
                    {
                        Name = part,
                        IsFolder = !isLast,
                        IsCurrent = isLast && branch.IsCurrent,
                        Branch = isLast ? branch : null
                    };
                    currentLevel.Add(existingNode);
                }

                currentLevel = existingNode.Children;
            }
        }

        SortNodes(rootNodes);
    }

    private void SortNodes(ObservableCollection<GitBranchNode> nodes)
    {
        var sorted = nodes.OrderByDescending(n => n.IsFolder).ThenBy(n => n.Name).ToList();
        nodes.Clear();
        foreach (var n in sorted)
        {
            SortNodes(n.Children);
            nodes.Add(n);
        }
    }

    private async Task RefreshSelectedCommitDetailsAsync()
    {
        if (SelectedCommit == null) { SelectedCommitFiles.Clear(); CommitFilesTree.Clear(); return; }
        var repo = GetRepoPath(); if (repo == null) return;
        var raw = await RunGitAsync(repo, $"show --name-status --pretty=format: {SelectedCommit.Hash}");
        Dispatcher.UIThread.Post(() =>
        {
            SelectedCommitFiles.Clear();
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 2)
                    SelectedCommitFiles.Add(new GitFileChange { Status = p[0], FilePath = p[1] });
            }

            // بناء شجرة المجلدات
            BuildCommitFilesTree(repo ?? "");
        });
    }

    private void BuildCommitFilesTree(string repoPath)
    {
        CommitFilesTree.Clear();
        if (SelectedCommitFiles.Count == 0) return;

        // Root node = repo folder name
        var root = new GitChangeNode
        {
            Name = Path.GetFileName(repoPath.TrimEnd(Path.DirectorySeparatorChar)),
            IsFolder = true,
            FullPath = repoPath
        };

        foreach (var file in SelectedCommitFiles)
        {
            var parts = file.FilePath.Replace('\\', '/').Split('/');
            var current = root;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                bool last = i == parts.Length - 1;

                if (last)
                {
                    current.Children.Add(new GitChangeNode
                    {
                        Name = part,
                        IsFolder = false,
                        Status = file.Status,
                        FullPath = Path.Combine(repoPath, file.FilePath),
                        FilePath = file.FilePath
                    });
                }
                else
                {
                    var folder = current.Children
                        .FirstOrDefault(n => n.IsFolder && n.Name == part);
                    if (folder == null)
                    {
                        folder = new GitChangeNode
                        {
                            Name = part,
                            IsFolder = true,
                            FullPath = Path.Combine(repoPath,
                                string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(i + 1)))
                        };
                        current.Children.Add(folder);
                    }
                    current = folder;
                }
            }
        }

        if (root.Children.Count > 0)
            CommitFilesTree.Add(root);
    }

    // ── Branches operations ───────────────────────────────────────────────────
    public async Task CreateBranchAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBranchName)) return;
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"checkout -b \"{NewBranchName.Trim()}\"");
        NewBranchName = "";
        await RefreshAsync();
    }

    public async Task CreateBranchFromAsync(string fromBranch, string newName)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"checkout -b \"{newName}\" \"{fromBranch}\"");
        await RefreshAsync();
    }

    public async Task CheckoutBranchAsync(GitBranch branch)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        var name = branch.IsRemote ? branch.Name.Replace("origin/", "") : branch.Name;
        await RunGitAsync(repo, $"checkout \"{name}\"");
        await RefreshAsync();
    }

    public async Task DeleteBranchAsync(GitBranch branch)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"branch -d \"{branch.Name}\"");
        await RefreshAsync();
    }

    public async Task MergeBranchAsync(GitBranch branch)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"merge \"{branch.Name}\"");
        await RefreshAsync();
    }

    public async Task RebaseBranchAsync(GitBranch branch)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"rebase \"{branch.Name}\"");
        await RefreshAsync();
    }

    public async Task RenameBranchAsync(GitBranch branch, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"branch -m \"{branch.Name}\" \"{newName.Trim()}\"");
        await RefreshAsync();
    }

    // ── Fetch / Pull / Push ───────────────────────────────────────────────────
    public async Task FetchAsync()
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, "fetch --all --prune");
        await RefreshAsync();
    }

    public async Task PullAsync()
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, "pull");
        await RefreshAsync();
    }

    public async Task PreparePushAsync()
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        var raw = await RunGitAsync(repo, "log @{u}..HEAD --pretty=format:%H|%s|%an|%ai");
        Dispatcher.UIThread.Post(() =>
        {
            PendingPushCommits.Clear();
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Split('|');
                if (p.Length >= 4)
                    PendingPushCommits.Add(new GitCommit
                    {
                        Hash = p[0],
                        Message = p[1],
                        Author = p[2],
                        Date = DateTime.TryParse(p[3], out var d) ? d : DateTime.MinValue
                    });
            }
            ShowPushDialog = true;
        });
        IsBusy = false;
    }

    public async Task PushAsync()
    {
        var repo = GetRepoPath(); 
        if (repo == null) 
        {
            // If no repo, definitely show the create dialog (it will offer to init)
            var projectPath = GetProjectFolder();
            if (projectPath != null)
                MainWindowViewModel.Instance.Factory.OpenCreateGitRepository(projectPath);
            return;
        }

        IsBusy = true;
        
        // Check if remotes exist
        var remotes = await RunGitAsync(repo, "remote");
        if (string.IsNullOrWhiteSpace(remotes))
        {
            IsBusy = false;
            MainWindowViewModel.Instance.Factory.OpenCreateGitRepository(repo);
            return;
        }

        ShowPushDialog = false;
        await RunGitAsync(repo, "push");
        await RefreshAsync();
    }

    public async Task ForcePushAsync()
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        ShowPushDialog = false;
        await RunGitAsync(repo, "push --force-with-lease");
        await RefreshAsync();
    }

    // ── Stash ─────────────────────────────────────────────────────────────────
    public async Task StashAsync()
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        var msg = string.IsNullOrWhiteSpace(StashMessage) ? "" : $" -m \"{StashMessage.Trim()}\"";
        await RunGitAsync(repo, $"stash push{msg}");
        StashMessage = "";
        await RefreshAsync();
    }

    public async Task StashPopAsync()
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        var idx = SelectedStash?.Index ?? "stash@{0}";
        await RunGitAsync(repo, $"stash pop {idx}");
        await RefreshAsync();
    }

    public async Task StashApplyAsync()
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        var idx = SelectedStash?.Index ?? "stash@{0}";
        await RunGitAsync(repo, $"stash apply {idx}");
        await RefreshAsync();
    }

    public async Task StashDropAsync()
    {
        if (SelectedStash == null) return;
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"stash drop {SelectedStash.Index}");
        await RefreshAsync();
    }

    // ── History & Blame ───────────────────────────────────────────────────────
    public async Task LoadFileHistoryAsync(string filePath)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        HistoryFilePath = filePath;
        IsBusy = true;
        var raw = await RunGitAsync(repo, $"log --follow --pretty=format:%H|%s|%an|%ai -- \"{filePath}\"");
        Dispatcher.UIThread.Post(() =>
        {
            FileHistory.Clear();
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Split('|');
                if (p.Length >= 4)
                    FileHistory.Add(new GitFileHistoryEntry
                    {
                        Hash = p[0],
                        Message = p[1],
                        Author = p[2],
                        Date = DateTime.TryParse(p[3], out var d) ? d : DateTime.MinValue
                    });
            }
        });
        IsBusy = false;
    }

    public async Task LoadBlameAsync(string filePath)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        var raw = await RunGitAsync(repo, $"blame --line-porcelain \"{filePath}\"");
        // Parse into readable format: "hash author line"
        var lines = raw.Split('\n');
        var result = new System.Text.StringBuilder();
        string curHash = "", curAuthor = "";
        foreach (var l in lines)
        {
            if (l.Length >= 40 && !l.StartsWith("\t")) curHash = l[..8];
            else if (l.StartsWith("author ")) curAuthor = l[7..];
            else if (l.StartsWith("\t"))
                result.AppendLine($"{curHash}  {curAuthor,-20}  {l[1..]}");
        }
        Dispatcher.UIThread.Post(() => BlameOutput = result.ToString());
        IsBusy = false;
    }

    // ── Conflicts ─────────────────────────────────────────────────────────────
    public async Task AcceptOursAsync(GitConflictFile file)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        await RunGitAsync(repo, $"checkout --ours \"{file.FilePath}\"");
        await RunGitAsync(repo, $"add \"{file.FilePath}\"");
        await RefreshAsync();
    }

    public async Task AcceptTheirsAsync(GitConflictFile file)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        await RunGitAsync(repo, $"checkout --theirs \"{file.FilePath}\"");
        await RunGitAsync(repo, $"add \"{file.FilePath}\"");
        await RefreshAsync();
    }

    public async Task AbortMergeAsync()
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, "merge --abort");
        await RefreshAsync();
    }

    // ── Undo / Revert ─────────────────────────────────────────────────────────
    public async Task UndoLastCommitAsync()
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, "reset --soft HEAD~1");
        await RefreshAsync();
    }

    public async Task RevertCommitAsync(GitCommit commit)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"revert --no-edit {commit.Hash}");
        await RefreshAsync();
    }

    public async Task CherryPickAsync(GitCommit commit)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"cherry-pick {commit.Hash}");
        await RefreshAsync();
    }

    // ── New methods for context menu ──────────────────────────────────────────
    public async Task CopyHashAsync(GitCommit commit)
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (topLevel != null)
            await Avalonia.Controls.TopLevel.GetTopLevel(topLevel)!.Clipboard!.SetTextAsync(commit.Hash);
        Dispatcher.UIThread.Post(() => ConsoleOutput.Add($"Copied: {commit.Hash}"));
    }

    public async Task NewBranchFromCommitAsync(GitCommit commit)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        var name = $"branch-from-{commit.ShortHash}";
        IsBusy = true;
        await RunGitAsync(repo, $"checkout -b \"{name}\" {commit.Hash}");
        await RefreshAsync();
    }

    public async Task EditCommitMessageAsync(GitCommit commit)
    {
        // Only works for the last commit (HEAD)
        if (commit.Hash != Commits.FirstOrDefault()?.Hash) return;
        var repo = GetRepoPath(); if (repo == null) return;
        // Store new message in CommitMessage for user to edit, then amend
        Dispatcher.UIThread.Post(() => ConsoleOutput.Add("Use 'git commit --amend' to edit the message"));
    }

    // ── Additional context menu actions ───────────────────────────────────────
    public async Task ResetToCommitAsync(GitCommit commit, string mode = "mixed")
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"reset --{mode} {commit.Hash}");
        await RefreshAsync();
    }

    public async Task CheckoutRevisionAsync(GitCommit commit)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"checkout {commit.Hash}");
        await RefreshAsync();
    }

    public async Task CreateTagAsync(GitCommit commit, string tagName)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"tag \"{tagName}\" {commit.Hash}");
        await RefreshAsync();
    }

    public async Task SquashIntoAsync(GitCommit commit)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"rebase -i {commit.Hash}^");
        await RefreshAsync();
    }

    public async Task PushUpToHereAsync(GitCommit commit)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        var branch = CurrentBranch;
        await RunGitAsync(repo, $"push origin {commit.Hash}:refs/heads/{branch}");
        await RefreshAsync();
    }

    public async Task GoToParentAsync(GitCommit commit)
    {
        if (commit.Parents.Count == 0) return;
        var parent = Commits.FirstOrDefault(c => c.Hash == commit.Parents[0]);
        if (parent != null) SelectedCommit = parent;
    }

    public async Task GoToChildAsync(GitCommit commit)
    {
        var child = Commits.FirstOrDefault(c => c.Parents.Contains(commit.Hash));
        if (child != null) SelectedCommit = child;
    }

    public Task ToggleLocalExpanded() { LocalExpanded = !LocalExpanded; return Task.CompletedTask; }
    public Task ToggleRemoteExpanded() { RemoteExpanded = !RemoteExpanded; return Task.CompletedTask; }
    public Task ToggleTagsExpanded() { TagsExpanded = !TagsExpanded; return Task.CompletedTask; }

    public async Task NewBranchFromSelectedAsync(GitBranch branch)
    {
        NewBranchName = $"new-from-{branch.Name.Replace("/", "-")}";
        await CreateBranchFromAsync(branch.Name, NewBranchName);
    }

    public async Task RenameBranchDialogAsync(GitBranch branch)
    {
        // Handled in code-behind via BranchPopup
        await RenameBranchAsync(branch, branch.Name + "-renamed");
    }

    public async Task NewTagFromCommitAsync(GitCommit commit)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        var tag = $"v-{commit.ShortHash}";
        await CreateTagAsync(commit, tag);
    }

    public async Task OpenOnGitHubAsync(GitCommit commit)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        var remoteUrl = await RunGitAsync(repo, "remote get-url origin");
        remoteUrl = remoteUrl.Trim()
            .Replace("git@github.com:", "https://github.com/")
            .Replace(".git", "");
        if (!string.IsNullOrEmpty(remoteUrl))
        {
            var url = $"{remoteUrl}/commit/{commit.Hash}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    // ── Init / Remote ─────────────────────────────────────────────────────────
    public async Task InitRepoAsync()
    {
        var repo = GetRepoPath() ?? GetProjectFolder();
        if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, "init");
        await RunGitAsync(repo, "add .");
        await RunGitAsync(repo, "commit -m \"Initial commit\"");
        await RefreshAsync();
    }

    public async Task AddRemoteAsync(string name, string url)
    {
        var repo = GetRepoPath(); if (repo == null) return;
        IsBusy = true;
        await RunGitAsync(repo, $"remote add {name} {url}");
        IsBusy = false;
    }

    public async Task ShareOnGitHubAsync(string url)
    {
        var repo = GetRepoPath() ?? GetProjectFolder();
        if (repo == null) return;
        IsBusy = true;
        try
        {
            if (!Directory.Exists(Path.Combine(repo, ".git")))
            {
                await RunGitAsync(repo, "init");
                await RunGitAsync(repo, "add .");
                await RunGitAsync(repo, "commit -m \"Initial commit\"");
            }
            await RunGitAsync(repo, $"remote add origin {url}");
            await RunGitAsync(repo, "push -u origin master");
            Dispatcher.UIThread.Post(() => ConsoleOutput.Add($"Shared on GitHub: {url}"));
            await RefreshAsync();
        }
        catch (Exception ex) { Dispatcher.UIThread.Post(() => ConsoleOutput.Add("ERROR: " + ex.Message)); }
        finally { IsBusy = false; }
    }

    public void RefreshRepositories()
    {
        if (MainWindowViewModel.Instance?.SolutionTree == null) return;

        var candidates = new List<string>();
        foreach (var node in MainWindowViewModel.Instance.SolutionTree) CollectPaths(node, candidates);

        var repos = new HashSet<string>();
        foreach (var path in candidates)
        {
            var cur = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            while (!string.IsNullOrEmpty(cur))
            {
                if (Directory.Exists(Path.Combine(cur, ".git")))
                {
                    repos.Add(cur);
                    break;
                }
                var parent = Path.GetDirectoryName(cur);
                if (parent == cur) break;
                cur = parent;
            }
        }

        Dispatcher.UIThread.Post(() =>
        {
            DiscoveredRepositories.Clear();
            foreach (var r in repos) DiscoveredRepositories.Add(r);
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    public string? GetRepoPath()
    {
        if (!string.IsNullOrEmpty(SelectedRepositoryPath)) return SelectedRepositoryPath;

        if (MainWindowViewModel.Instance?.SolutionTree == null || MainWindowViewModel.Instance.SolutionTree.Count == 0) return null;
        var candidates = new List<string>();
        foreach (var node in MainWindowViewModel.Instance.SolutionTree) CollectPaths(node, candidates);
        foreach (var path in candidates)
        {
            var cur = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            while (!string.IsNullOrEmpty(cur))
            {
                if (Directory.Exists(Path.Combine(cur, ".git"))) return cur;
                var parent = Path.GetDirectoryName(cur);
                if (parent == cur) break;
                cur = parent;
            }
        }
        return null;
    }

    public string? GetProjectFolder()
    {
        var candidates = new List<string>();
        foreach (var node in MainWindowViewModel.Instance.SolutionTree) CollectPaths(node, candidates);
        return candidates.Select(p => File.Exists(p) ? Path.GetDirectoryName(p) : p)
                         .FirstOrDefault(d => !string.IsNullOrEmpty(d));
    }

    private static void CollectPaths(SolutionNode node, List<string> paths)
    {
        if (!string.IsNullOrEmpty(node.FilePath)) paths.Add(node.FilePath);
        if (node.Children != null) foreach (var c in node.Children) CollectPaths(c, paths);
    }

    private async Task<string> RunGitAsync(string workingDir, string arguments)
    {
        try
        {
            Dispatcher.UIThread.Post(() => ConsoleOutput.Add($"> git {arguments}"));
            var si = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(si);
            if (p == null) return "";
            var output = await p.StandardOutput.ReadToEndAsync();
            var error = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            if (!string.IsNullOrEmpty(output)) Dispatcher.UIThread.Post(() => ConsoleOutput.Add(output.TrimEnd()));
            if (!string.IsNullOrEmpty(error)) Dispatcher.UIThread.Post(() => ConsoleOutput.Add("ERR: " + error.TrimEnd()));
            IsBusy = false;
            return output;
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => ConsoleOutput.Add("EXCEPTION: " + ex.Message));
            IsBusy = false;
            return "";
        }
    }
}
