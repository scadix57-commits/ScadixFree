using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace Scadix.Designer.ViewModels.Tools;

// ── Terminal Session ──────────────────────────────────────────────────────────
// Each TerminalSession is an independent shell process with its own history.
// This allows multiple concurrent terminals without a shared global state.
public partial class TerminalSession : ObservableObject, IDisposable
{
    private Process? _process;
    private StreamWriter? _stdin;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty] private string _title = "Terminal";
    [ObservableProperty] private string _input = "";
    [ObservableProperty] private bool _isAlive;
    [ObservableProperty] private bool _showWelcome = true;

    // Output is a ring-buffer limited to MaxLines to prevent unbounded memory growth.
    private const int MaxLines = 2000;
    public ObservableCollection<TerminalLine> Output { get; } = new();

    // Command history for ↑/↓ navigation (like a real shell).
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    public TerminalSession(string? workingDir = null)
    {
        WorkingDir = workingDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Title = $"Terminal ({Path.GetFileName(WorkingDir) ?? "~"})";
    }

    public string WorkingDir { get; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task StartAsync()
    {
        if (IsAlive) return;
        _cts = new CancellationTokenSource();

        var shell = ResolveShell();
        var psi = new ProcessStartInfo
        {
            FileName               = shell.Path,
            Arguments              = shell.Args,
            WorkingDirectory       = Directory.Exists(WorkingDir) ? WorkingDir : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };

        try
        {
            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Exited += OnProcessExited;
            _process.Start();

            _stdin = _process.StandardInput;
            _stdin.AutoFlush = true;

            IsAlive = true;
            ShowWelcome = false;

            Append($"[{shell.Name} started — {WorkingDir}]", TerminalLineKind.System);

            _ = PumpStreamAsync(_process.StandardOutput, TerminalLineKind.StdOut, _cts.Token);
            _ = PumpStreamAsync(_process.StandardError, TerminalLineKind.StdErr, _cts.Token);
        }
        catch (Exception ex)
        {
            Append($"[Failed to start shell: {ex.Message}]", TerminalLineKind.Error);
        }

        await Task.CompletedTask;
    }

    public void Stop()
    {
        if (_disposed) return;
        _cts.Cancel();
        try { _stdin?.Close(); } catch { }
        try { _process?.Kill(entireProcessTree: true); } catch { }
        IsAlive = false;
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    public void SendInput()
    {
        if (!IsAlive || _stdin == null) return;

        var cmd = Input.Trim();
        Input = "";

        if (!string.IsNullOrEmpty(cmd))
        {
            if (_history.Count == 0 || _history[^1] != cmd)
                _history.Add(cmd);
            _historyIndex = _history.Count;
        }

        try { _stdin.WriteLine(cmd); }
        catch (Exception ex) { Append($"[Write error: {ex.Message}]", TerminalLineKind.Error); }
    }

    [RelayCommand]
    public void HistoryUp()
    {
        if (_history.Count == 0) return;
        _historyIndex = Math.Max(0, _historyIndex - 1);
        Input = _history[_historyIndex];
    }

    [RelayCommand]
    public void HistoryDown()
    {
        if (_history.Count == 0) return;
        _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
        Input = _historyIndex < _history.Count ? _history[_historyIndex] : "";
    }

    [RelayCommand]
    public void ClearScreen() => Dispatcher.UIThread.Post(Output.Clear);

    // ── Output pump ───────────────────────────────────────────────────────────

    private async Task PumpStreamAsync(StreamReader reader, TerminalLineKind kind, CancellationToken ct)
    {
        var buf = new char[512];
        var sb  = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                int read = await reader.ReadAsync(buf, 0, buf.Length).WaitAsync(ct);
                if (read == 0) break;

                sb.Append(buf, 0, read);

                // Flush complete lines; keep incomplete tail in buffer.
                var text = sb.ToString();
                var lastNl = text.LastIndexOf('\n');
                if (lastNl >= 0)
                {
                    var complete = text[..(lastNl + 1)];
                    sb.Clear();
                    sb.Append(text[(lastNl + 1)..]);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var line in complete.Split('\n'))
                        {
                            var trimmed = line.TrimEnd('\r');
                            if (trimmed.Length > 0)
                                Append(trimmed, kind);
                        }
                    });
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                Append($"[Stream error: {ex.Message}]", TerminalLineKind.Error));
        }
    }

    private void Append(string text, TerminalLineKind kind)
    {
        // Ring buffer — keep memory bounded.
        while (Output.Count >= MaxLines)
            Output.RemoveAt(0);
        Output.Add(new TerminalLine(text, kind));
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        _cts.Cancel();
        var code = _process?.ExitCode ?? -1;
        Dispatcher.UIThread.Post(() =>
        {
            IsAlive = false;
            Append($"[Process exited with code {code}]", TerminalLineKind.System);
        });
    }

    // ── Shell resolution ──────────────────────────────────────────────────────

    private static (string Path, string Args, string Name) ResolveShell()
    {
        // Priority: PowerShell 7 → Windows PowerShell → CMD
        var pwsh7 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell", "7", "pwsh.exe");
        if (File.Exists(pwsh7)) return (pwsh7, "-NoLogo -NoExit", "pwsh");

        var ps5 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(ps5)) return (ps5, "-NoLogo -NoExit", "powershell");

        return ("cmd.exe", "", "cmd");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _process?.Dispose();
        _cts.Dispose();
    }
}

// ── Terminal Line Model ───────────────────────────────────────────────────────
public enum TerminalLineKind { StdOut, StdErr, System, Error }

public class TerminalLine
{
    public string Text { get; }
    public TerminalLineKind Kind { get; }
    public TerminalLine(string text, TerminalLineKind kind) { Text = text; Kind = kind; }
}

// ── TerminalViewModel (Tool Panel) ───────────────────────────────────────────
// Manages a collection of sessions; supports opening new sessions and switching.
public partial class TerminalViewModel : Tool
{
    public static TerminalViewModel? Current { get; private set; }

    [ObservableProperty]
    private TerminalSession? _activeSession;

    public ObservableCollection<TerminalSession> Sessions { get; } = new();

    public TerminalViewModel()
    {
        Id       = "Terminal";
        Title    = "Terminal";
        CanClose = true;
        Current  = this;
    }

    // ── Session management ────────────────────────────────────────────────────

    [RelayCommand]
    public async Task NewSessionAsync()
    {
        var workDir = GetProjectWorkDir();
        var session = new TerminalSession(workDir);
        Sessions.Add(session);
        ActiveSession = session;
        await session.StartAsync();
    }

    [RelayCommand]
    public void CloseSession(TerminalSession session)
    {
        session.Stop();
        session.Dispose();
        Sessions.Remove(session);
        ActiveSession = Sessions.Count > 0 ? Sessions[^1] : null;
    }

    [RelayCommand]
    public void SwitchSession(TerminalSession session)
    {
        ActiveSession = session;
    }

    // Open a terminal automatically when tool is activated for the first time.
    public async Task EnsureActiveSessionAsync()
    {
        if (Sessions.Count == 0)
            await NewSessionAsync();
        else if (ActiveSession == null)
            ActiveSession = Sessions[0];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetProjectWorkDir()
    {
        // Use the solution root if a project is loaded, otherwise user home.
        var tree = MainWindowViewModel.Instance?.SolutionTree;
        if (tree != null && tree.Count > 0)
        {
            var fp = tree[0].FilePath;
            if (!string.IsNullOrEmpty(fp))
            {
                var dir = File.Exists(fp) ? Path.GetDirectoryName(fp) : fp;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return dir;
            }
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
