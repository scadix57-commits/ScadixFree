using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace Scadix.Designer.ViewModels.Tools;

// ── Reference Result Models ───────────────────────────────────────────────────

public class ReferenceLocation
{
    public string FilePath   { get; init; } = "";
    public string FileName   { get; init; } = "";
    public int    Line       { get; init; }
    public int    Column     { get; init; }
    public string LineText   { get; init; } = "";

    // Pre-split for highlight rendering in the view
    public string TextBefore { get; init; } = "";
    public string MatchText  { get; init; } = "";
    public string TextAfter  { get; init; } = "";
}

public class ReferenceGroup
{
    public string FilePath  { get; init; } = "";
    public string FileName  { get; init; } = "";
    public string Directory { get; init; } = "";
    public ObservableCollection<ReferenceLocation> Locations { get; } = new();
    public int Count => Locations.Count;
}

// ── FindAllReferencesViewModel ────────────────────────────────────────────────
/// <summary>
/// Philosophy: real-time grep-based search across all text files in the solution.
/// Uses System.Text.RegularExpressions for fast parallel scanning.
/// Groups results by file for clean presentation (like VS / Rider behavior).
/// Supports navigating to any result by raising a NavigateToFile event.
/// </summary>
public partial class FindAllReferencesViewModel : Tool
{
    public static FindAllReferencesViewModel? Current { get; private set; }

    // ── State ─────────────────────────────────────────────────────────────────

    [ObservableProperty] private string  _query           = "";
    [ObservableProperty] private bool    _isSearching;
    [ObservableProperty] private bool    _matchCase       = false;
    [ObservableProperty] private bool    _wholeWord       = false;
    [ObservableProperty] private bool    _useRegex        = false;
    [ObservableProperty] private bool    _hasResults;
    [ObservableProperty] private string  _statusText      = "Ready";
    [ObservableProperty] private int     _totalMatches;
    [ObservableProperty] private int     _totalFiles;
    [ObservableProperty] private ReferenceLocation? _selectedResult;

    public ObservableCollection<ReferenceGroup> Groups { get; } = new();

    // Debounce search while user is typing
    private CancellationTokenSource? _searchCts;
    private DateTime _lastType = DateTime.MinValue;

    // Event raised when user double-clicks a result
    public event Action<string, int, int>? NavigateRequested;

    public FindAllReferencesViewModel()
    {
        Id       = "FindReferences";
        Title    = "Find All References";
        CanClose = true;
        Current  = this;
    }

    // ── Public API — called externally to search for a symbol ─────────────────

    public void SearchFor(string symbol)
    {
        Query = symbol;
        _ = RunSearchAsync(symbol);
    }

    // ── Query change with debounce ─────────────────────────────────────────────

    partial void OnQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Groups.Clear();
            HasResults  = false;
            StatusText  = "Ready";
            TotalMatches = 0;
            TotalFiles   = 0;
            return;
        }

        _lastType = DateTime.UtcNow;
        _ = DebounceSearchAsync(value);
    }

    private async Task DebounceSearchAsync(string value)
    {
        await Task.Delay(400);
        if (DateTime.UtcNow - _lastType >= TimeSpan.FromMilliseconds(390))
            await RunSearchAsync(value);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RunSearchAsync(string? overrideQuery = null)
    {
        var q = overrideQuery ?? Query;
        if (string.IsNullOrWhiteSpace(q)) return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        IsSearching  = true;
        StatusText   = "Searching...";
        TotalMatches = 0;
        TotalFiles   = 0;

        await Dispatcher.UIThread.InvokeAsync(Groups.Clear);

        try
        {
            var roots = GetSearchRoots();
            if (roots.Length == 0)
            {
                StatusText = "No project loaded — open a solution first.";
                return;
            }

            // Build regex once
            var pattern = BuildPattern(q);
            if (pattern == null)
            {
                StatusText = "Invalid regular expression.";
                return;
            }

            // Collect all searchable files
            var extensions = new[] { ".cs", ".axaml", ".xaml", ".xml", ".json", ".md", ".txt" };
            var files = roots
                .SelectMany(r => Directory.Exists(r)
                    ? Directory.EnumerateFiles(r, "*.*", SearchOption.AllDirectories)
                    : Enumerable.Empty<string>())
                .Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Distinct()
                .ToList();

            StatusText = $"Scanning {files.Count} files...";

            int matchCount = 0;
            int fileCount  = 0;

            // Parallel scan — produce results in batches
            await Task.Run(async () =>
            {
                await Parallel.ForEachAsync(files, new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken      = ct
                },
                async (file, _) =>
                {
                    ct.ThrowIfCancellationRequested();

                    string content;
                    try { content = await File.ReadAllTextAsync(file, ct); }
                    catch { return; }

                    var matches = ScanFile(file, content, pattern, q);
                    if (matches.Count == 0) return;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var group = new ReferenceGroup
                        {
                            FilePath  = file,
                            FileName  = Path.GetFileName(file),
                            Directory = Path.GetDirectoryName(file) ?? "",
                        };
                        foreach (var m in matches)
                            group.Locations.Add(m);

                        Groups.Add(group);
                        matchCount += matches.Count;
                        fileCount++;
                        TotalMatches = matchCount;
                        TotalFiles   = fileCount;
                        StatusText   = $"{matchCount} result(s) in {fileCount} file(s)";
                    });
                });
            }, ct);

            HasResults = Groups.Count > 0;
            StatusText = Groups.Count == 0
                ? $"No results for '{q}'"
                : $"{TotalMatches} result(s) in {TotalFiles} file(s)";
        }
        catch (OperationCanceledException) { StatusText = "Search cancelled."; }
        catch (Exception ex)              { StatusText = $"Error: {ex.Message}"; }
        finally                           { IsSearching = false; }
    }

    [RelayCommand]
    private void ClearResults()
    {
        Query = "";
        Groups.Clear();
        HasResults   = false;
        StatusText   = "Ready";
        TotalMatches = 0;
        TotalFiles   = 0;
    }

    [RelayCommand]
    private void NavigateTo(ReferenceLocation loc)
    {
        SelectedResult = loc;
        NavigateRequested?.Invoke(loc.FilePath, loc.Line, loc.Column);

        // Also open via MainWindowViewModel
        try { MainWindowViewModel.Instance?.Open(loc.FilePath); }
        catch { }
    }

    // ── File scanner ──────────────────────────────────────────────────────────

    private List<ReferenceLocation> ScanFile(string path, string content, Regex pattern, string rawQuery)
    {
        var results = new List<ReferenceLocation>();
        var lines   = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var m    = pattern.Match(line);
            while (m.Success)
            {
                results.Add(new ReferenceLocation
                {
                    FilePath   = path,
                    FileName   = Path.GetFileName(path),
                    Line       = i + 1,
                    Column     = m.Index + 1,
                    LineText   = line.Trim(),
                    TextBefore = line[..m.Index].TrimStart(),
                    MatchText  = m.Value,
                    TextAfter  = line[(m.Index + m.Length)..],
                });
                m = m.NextMatch();
            }
        }

        return results;
    }

    // ── Pattern builder ───────────────────────────────────────────────────────

    private Regex? BuildPattern(string query)
    {
        try
        {
            var flags = MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            flags |= RegexOptions.Compiled;

            var pat = UseRegex
                ? query
                : WholeWord
                    ? $@"\b{Regex.Escape(query)}\b"
                    : Regex.Escape(query);

            return new Regex(pat, flags);
        }
        catch { return null; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string[] GetSearchRoots()
    {
        var tree = MainWindowViewModel.Instance?.SolutionTree;
        if (tree == null || tree.Count == 0) return Array.Empty<string>();

        return tree
            .Select(n => File.Exists(n.FilePath) ? Path.GetDirectoryName(n.FilePath) : n.FilePath)
            .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
            .Distinct()
            .ToArray()!;
    }
}
