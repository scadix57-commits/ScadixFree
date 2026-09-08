using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Scadix.Designer.Services;

/// <summary>
/// Central service to manage breakpoints across the application.
/// Used to synchronize XamlEditor gutters and provide data to DebuggerService.
/// </summary>
public class BreakpointService
{
    private static readonly Lazy<BreakpointService> _instance = new(() => new BreakpointService());
    public static BreakpointService Instance => _instance.Value;

    // Dictionary: FilePath -> Set of active line numbers (1-indexed)
    private readonly ConcurrentDictionary<string, HashSet<int>> _breakpoints = new();
    private string? _currentProjectPath;

    public event EventHandler<BreakpointChangedEventArgs>? BreakpointsChanged;

    private BreakpointService() { }

    public void AddBreakpoint(string filePath, int lineNumber)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var fileBreakpoints = _breakpoints.GetOrAdd(filePath, _ => new HashSet<int>());
        lock (fileBreakpoints)
        {
            if (fileBreakpoints.Add(lineNumber))
            {
                OnBreakpointsChanged(filePath);
                AutoSave();
            }
        }
    }

    public void RemoveBreakpoint(string filePath, int lineNumber)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        if (_breakpoints.TryGetValue(filePath, out var fileBreakpoints))
        {
            lock (fileBreakpoints)
            {
                if (fileBreakpoints.Remove(lineNumber))
                {
                    OnBreakpointsChanged(filePath);
                    AutoSave();
                }
            }
        }
    }

    public void ToggleBreakpoint(string filePath, int lineNumber)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var fileBreakpoints = _breakpoints.GetOrAdd(filePath, _ => new HashSet<int>());
        lock (fileBreakpoints)
        {
            if (fileBreakpoints.Contains(lineNumber))
                fileBreakpoints.Remove(lineNumber);
            else
                fileBreakpoints.Add(lineNumber);

            OnBreakpointsChanged(filePath);
            AutoSave();
        }
    }

    public IReadOnlyList<int> GetBreakpoints(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return ImmutableList<int>.Empty;

        if (_breakpoints.TryGetValue(filePath, out var fileBreakpoints))
        {
            lock (fileBreakpoints)
            {
                return fileBreakpoints.OrderBy(n => n).ToImmutableList();
            }
        }
        return ImmutableList<int>.Empty;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<int>> GetAllBreakpoints()
    {
        return _breakpoints.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<int>)kvp.Value.OrderBy(n => n).ToList()
        );
    }

    public void ClearAll()
    {
        var paths = _breakpoints.Keys.ToList();
        _breakpoints.Clear();
        foreach (var path in paths)
        {
            OnBreakpointsChanged(path);
        }
        AutoSave();
    }

    // ── Persistence ───────────────────────────────────────────────────────

    public void Load(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath)) return;
        _currentProjectPath = projectPath;

        try
        {
            var root = Directory.Exists(projectPath) ? projectPath : Path.GetDirectoryName(projectPath);
            if (string.IsNullOrEmpty(root)) return;

            var file = Path.Combine(root, ".scadix", "breakpoints.json");
            if (!File.Exists(file)) return;

            var json = File.ReadAllText(file);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(json);
            if (data == null) return;

            _breakpoints.Clear();
            foreach (var kvp in data)
            {
                _breakpoints.TryAdd(kvp.Key, new HashSet<int>(kvp.Value));
                // Notify UI to refresh gutters
                OnBreakpointsChanged(kvp.Key);
            }
        }
        catch { /* fail silently on load */ }
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(_currentProjectPath)) return;

        try
        {
            var root = Directory.Exists(_currentProjectPath) ? _currentProjectPath : Path.GetDirectoryName(_currentProjectPath);
            if (string.IsNullOrEmpty(root)) return;

            var dir = Path.Combine(root, ".scadix");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "breakpoints.json");

            var data = _breakpoints.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()
            );

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
        }
        catch { /* fail silently on save */ }
    }

    private void AutoSave() => Save();

    private void OnBreakpointsChanged(string filePath)
    {
        BreakpointsChanged?.Invoke(this, new BreakpointChangedEventArgs(filePath, GetBreakpoints(filePath)));
    }
}

public class BreakpointChangedEventArgs : EventArgs
{
    public string FilePath { get; }
    public IReadOnlyList<int> Breakpoints { get; }

    public BreakpointChangedEventArgs(string filePath, IReadOnlyList<int> breakpoints)
    {
        FilePath = filePath;
        Breakpoints = breakpoints;
    }
}
