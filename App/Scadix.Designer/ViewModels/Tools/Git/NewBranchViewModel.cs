using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Scadix.Designer.ViewModels;

// ── Per-repo item shown in the checkboxes list ────────────────────────────────
public class NewBranchRepoItem : ObservableObject
{
    public string RepoPath     { get; set; } = "";
    public string RepoName     { get; set; } = "";
    public string CurrentBranch { get; set; } = "";

    // Display name shown in the Repository ComboBox
    public string DisplayName  => RepoName;

    private bool _isChecked = true;
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }
}

// ── ViewModel ─────────────────────────────────────────────────────────────────
public partial class NewBranchViewModel : ObservableObject
{
    private readonly GitRepositoriesViewModel _git;

    public event Action? RequestClose;

    // ── Branch name ───────────────────────────────────────────────────────────
    private string _branchName = "";
    public string BranchName
    {
        get => _branchName;
        set
        {
            SetProperty(ref _branchName, value);
            OnPropertyChanged(nameof(CanCreate));
            OnPropertyChanged(nameof(BranchNameBorderBrush));
        }
    }

    public IBrush BranchNameBorderBrush =>
        string.IsNullOrWhiteSpace(BranchName)
            ? Brushes.OrangeRed
            : Brush.Parse("#CCCCCC");

    // ── Repository selector (top ComboBox) ───────────────────────────────────
    public ObservableCollection<NewBranchRepoItem> Repositories { get; } = new();

    private NewBranchRepoItem? _selectedRepository;
    public NewBranchRepoItem? SelectedRepository
    {
        get => _selectedRepository;
        set
        {
            SetProperty(ref _selectedRepository, value);
            RefreshBaseBranches();
        }
    }

    // ── Based-on branch ───────────────────────────────────────────────────────
    public ObservableCollection<string> BaseBranches { get; } = new();

    private string? _selectedBaseBranch;
    public string? SelectedBaseBranch
    {
        get => _selectedBaseBranch;
        set => SetProperty(ref _selectedBaseBranch, value);
    }

    // ── Per-repo checkboxes ───────────────────────────────────────────────────
    public ObservableCollection<NewBranchRepoItem> RepoItems { get; } = new();

    // "Create N branches" header checkbox
    private bool _allReposChecked = true;
    public bool AllReposChecked
    {
        get => _allReposChecked;
        set
        {
            SetProperty(ref _allReposChecked, value);
            foreach (var item in RepoItems)
                item.IsChecked = value;
            RefreshCounts();
        }
    }

    // ── Checkout after create ─────────────────────────────────────────────────
    private bool _checkoutAfterCreate = true;
    public bool CheckoutAfterCreate
    {
        get => _checkoutAfterCreate;
        set => SetProperty(ref _checkoutAfterCreate, value);
    }

    // ── Derived display ───────────────────────────────────────────────────────
    public int    CheckedCount   => RepoItems.Count(r => r.IsChecked);
    public string BranchesLabel  => CheckedCount == 1 ? "branch" : "branches";
    public bool   CanCreate      => !string.IsNullOrWhiteSpace(BranchName) && CheckedCount > 0;

    // ── Constructor ───────────────────────────────────────────────────────────
    public NewBranchViewModel(GitRepositoriesViewModel git)
    {
        _git = git;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        // Build repo items from all discovered repositories
        var repos = _git.DiscoveredRepositories.ToList();

        // If none discovered yet, fall back to the single selected repo
        if (repos.Count == 0 && _git.GetRepoPath() is string single)
            repos.Add(single);

        foreach (var repoPath in repos)
        {
            var branch = await RunGitAsync(repoPath, "rev-parse --abbrev-ref HEAD");
            var item = new NewBranchRepoItem
            {
                RepoPath      = repoPath,
                RepoName      = Path.GetFileName(repoPath),
                CurrentBranch = branch.Trim()
            };
            // Watch checkbox changes to update counts
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NewBranchRepoItem.IsChecked))
                    RefreshCounts();
            };

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RepoItems.Add(item);
                Repositories.Add(item);
            });
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Default: select the repo that matches the current git selection
            SelectedRepository = Repositories.FirstOrDefault(r => r.RepoPath == _git.SelectedRepositoryPath)
                               ?? Repositories.FirstOrDefault();
            RefreshCounts();
        });
    }

    private void RefreshBaseBranches()
    {
        BaseBranches.Clear();
        if (SelectedRepository == null) return;

        // Add local branches for the selected repo
        foreach (var b in _git.LocalBranches)
            BaseBranches.Add(b.Name);

        // Default to current branch of selected repo
        SelectedBaseBranch = SelectedRepository.CurrentBranch;
        if (!BaseBranches.Contains(SelectedBaseBranch))
            SelectedBaseBranch = BaseBranches.FirstOrDefault();
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(CheckedCount));
        OnPropertyChanged(nameof(BranchesLabel));
        OnPropertyChanged(nameof(CanCreate));

        // Sync header checkbox state
        var all = RepoItems.All(r => r.IsChecked);
        var none = RepoItems.All(r => !r.IsChecked);
        if (_allReposChecked != all && !none)
        {
            _allReposChecked = all;
            OnPropertyChanged(nameof(AllReposChecked));
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task Create()
    {
        if (!CanCreate) return;

        var name     = BranchName.Trim();
        var baseBranch = SelectedBaseBranch ?? _git.CurrentBranch;
        var checkout = CheckoutAfterCreate;

        RequestClose?.Invoke();

        foreach (var item in RepoItems.Where(r => r.IsChecked))
        {
            if (checkout)
                await RunGitAsync(item.RepoPath, $"checkout -b \"{name}\" \"{baseBranch}\"");
            else
                await RunGitAsync(item.RepoPath, $"branch \"{name}\" \"{baseBranch}\"");
        }

        await _git.RefreshAsync();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    // ── Git helper ────────────────────────────────────────────────────────────
    private static async Task<string> RunGitAsync(string workingDir, string arguments)
    {
        var si = new ProcessStartInfo
        {
            FileName               = "git",
            Arguments              = arguments,
            WorkingDirectory       = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };
        using var p = Process.Start(si);
        if (p == null) return "";
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        return output;
    }
}
