using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Scadix.Designer.Models;
using Scadix.Designer.Services;

namespace Scadix.Designer.ViewModels;

// ── Document ViewModel ────────────────────────────────────────────────────────

/// <summary>
/// Document tab that shows a side-by-side diff for a changed file.
/// Left = Index (staged), Right = Working tree.
/// </summary>
public partial class GitDiffDocumentViewModel : Dock.Model.Mvvm.Controls.Document
{
    // ── Brushes ───────────────────────────────────────────────────────────────
    private static readonly IBrush AddedBg    = new SolidColorBrush(Color.FromArgb(60,  80, 200,  80));
    private static readonly IBrush RemovedBg  = new SolidColorBrush(Color.FromArgb(60, 200,  60,  60));
    private static readonly IBrush AddedFg    = new SolidColorBrush(Color.Parse("#5DA656"));
    private static readonly IBrush RemovedFg  = new SolidColorBrush(Color.Parse("#E06C75"));
    private static readonly IBrush NormalFg   = new SolidColorBrush(Color.Parse("#CCCCCC"));
    private static readonly IBrush EmptyBg    = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));

    // ── Properties ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _leftLabel    = "Index";
    [ObservableProperty] private string _rightLabel   = "Working tree";
    [ObservableProperty] private string _filePath     = "";
    [ObservableProperty] private bool   _isLoading    = true;
    [ObservableProperty] private string _leftText     = "";
    [ObservableProperty] private string _rightText    = "";
    [ObservableProperty] private int    _changesCount;
    [ObservableProperty] private int    _addedCount;
    [ObservableProperty] private int    _removedCount;

    public ObservableCollection<DiffLine> LeftLines  { get; } = new();
    public ObservableCollection<DiffLine> RightLines { get; } = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public GitDiffDocumentViewModel(GitChangeNode node, string repoPath)
    {
        var fileName = Path.GetFileName(node.FullPath ?? node.Name);
        Id    = $"Diff:{node.FullPath}";
        Title = $"Diff - {fileName}";

        FilePath    = node.FullPath ?? "";
        LeftLabel   = $"{fileName} (Index)";
        RightLabel  = $"{fileName} (Working tree)";

        _ = LoadDiffAsync(node.FilePath ?? node.Change?.FilePath ?? "", repoPath);
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    private async Task LoadDiffAsync(string relativePath, string repoPath)
    {
        IsLoading = true;
        try
        {
            // Left: staged version (git show :path)
            var leftText = await RunGitAsync(repoPath, $"show :{relativePath}");

            // Right: working tree (read file directly)
            var fullPath  = Path.Combine(repoPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var rightText = File.Exists(fullPath)
                ? await File.ReadAllTextAsync(fullPath)
                : "";

            // Build unified diff and split into left/right lines
            BuildSideBySide(leftText, rightText);
        }
        catch
        {
            // Not staged — show empty left, current file on right
            try
            {
                var fullPath = Path.Combine(repoPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var rightText = File.Exists(fullPath)
                    ? await File.ReadAllTextAsync(fullPath)
                    : "";
                BuildSideBySide("", rightText);
            }
            catch { }
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Side-by-side builder ──────────────────────────────────────────────────

    private void BuildSideBySide(string leftText, string rightText)
    {
        var result = DiffService.ComputeSideBySide(leftText, rightText);
        
        LeftLines.Clear();
        foreach (var line in result.Left) LeftLines.Add(line);
        
        RightLines.Clear();
        foreach (var line in result.Right) RightLines.Add(line);

        LeftText  = leftText;
        RightText = rightText;

        // Update counts
        int added = 0, removed = 0;
        foreach (var line in result.Right) if (line.Background != Brushes.Transparent && line.LineNumber != "") added++;
        foreach (var line in result.Left)  if (line.Background != Brushes.Transparent && line.LineNumber != "") removed++;
        
        AddedCount    = added;
        RemovedCount  = removed;
        ChangesCount  = added + removed;
    }

    // ── Git helper ────────────────────────────────────────────────────────────

    private static async Task<string> RunGitAsync(string workingDir, string arguments)
    {
        try
        {
            var si = new ProcessStartInfo("git", arguments)
            {
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
        catch { return ""; }
    }
}
