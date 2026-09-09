using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scadix.Designer.Services;
using Scadix.Designer;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scadix.Designer.ViewModels.Tools;

/// <summary>
/// Exposes debugger state and commands to the toolbar.
/// Bridges DebuggerService ↔ DiagnosticsService ↔ UI.
/// </summary>
public partial class DebugToolbarViewModel : ObservableObject
{
    private static DebugToolbarViewModel? _instance;
    public static DebugToolbarViewModel Instance => _instance ??= new DebugToolbarViewModel();

    // ── State ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isDebugging;
    [ObservableProperty] private bool   _isPaused;
    [ObservableProperty] private string _statusText   = "Ready";
    [ObservableProperty] private string _currentFile  = string.Empty;
    [ObservableProperty] private int    _currentLine;

    /// <summary>Fired when the debugger pauses — carries (filePath, lineNumber).</summary>
    public event EventHandler<(string File, int Line)>? ExecutionLineMoved;

    /// <summary>Fired when debugging stops — views should clear their highlights.</summary>
    public event EventHandler? ExecutionLineCleared;
    private int _stopVersion;

    private void ClearExecutionLocation()
    {
        CurrentFile = string.Empty;
        CurrentLine = 0;
        ExecutionLineCleared?.Invoke(this, EventArgs.Empty);
        NotifyEditorExecutionLine(string.Empty, -1);
        CallStackViewModel.Current?.Clear();
        LocalsViewModel.Current?.Clear();
    }

    private DebugToolbarViewModel()
    {
        var dbg = DebuggerService.Instance;

        dbg.DebuggingStarted += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            ++_stopVersion;
            IsDebugging = true;
            IsPaused    = false;
            StatusText  = "Debugging…";
            DiagnosticsService.Instance.StartInProcessMonitoring();
        });

        dbg.DebuggingStopped += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            ++_stopVersion;
            IsDebugging = false;
            IsPaused    = false;
            StatusText  = "Ready";
            ClearExecutionLocation();
            DiagnosticsService.Instance.StopMonitoring();
        });

        dbg.Continued += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            ++_stopVersion;
            IsPaused = false;
            StatusText = "Running…";
            ClearExecutionLocation();
        });

        dbg.Stopped += (_, e) => Dispatcher.UIThread.Post(() =>
        {
            var version = ++_stopVersion;
            ClearExecutionLocation();
            IsPaused   = true;
            StatusText = $"⏸ Paused — {e.Reason}";
            _ = OnStoppedAsync(e.ThreadId, version);
        });

        dbg.OutputReceived += (_, msg) =>
            BuildOutputService.Instance.AppendLine(msg);
    }

    // ── On stopped: fetch stack once, share with all panels ─────────────

    private async Task OnStoppedAsync(int threadId, int version)
    {
        try
        {
            var stackEl = await DebuggerService.Instance.StackTraceAsync(threadId);
            if (version != _stopVersion || !IsPaused) return;
            if (stackEl.ValueKind == System.Text.Json.JsonValueKind.Undefined) return;

            var frames = stackEl.GetProperty("stackFrames").EnumerateArray().ToList();
            if (frames.Count == 0) return;

            var topFrame   = frames[0];
            var topFrameId = topFrame.GetProperty("id").GetInt32();
            var topLine    = topFrame.GetProperty("line").GetInt32();
            string? topFile = null;
            if (topFrame.TryGetProperty("source", out var src) &&
                src.TryGetProperty("path", out var pathEl))
                topFile = pathEl.GetString();

            // 1. Highlight execution line in editor
            if (!string.IsNullOrEmpty(topFile) && topLine > 0)
            {
                CurrentFile = topFile;
                CurrentLine = topLine;
                StatusText  = $"⏸  {Path.GetFileName(topFile)}  line {topLine}";
                Dispatcher.UIThread.Post(() =>
                {
                    if (version != _stopVersion || !IsPaused) return;
                    ExecutionLineMoved?.Invoke(this, (topFile, topLine));
                    NotifyEditorExecutionLine(topFile, topLine);
                });
            }

            // 2. Populate Call Stack panel
            Dispatcher.UIThread.Post(() =>
            {
                if (version != _stopVersion || !IsPaused) return;
                var csVm = CallStackViewModel.Current;
                if (csVm != null)
                {
                    csVm.Frames.Clear();
                    foreach (var frame in frames)
                    {
                        var name   = frame.GetProperty("name").GetString() ?? "Unknown";
                        var source = frame.TryGetProperty("source", out var s)
                                     && s.TryGetProperty("path", out var p)
                                     ? p.GetString() : "Internal";
                        var line   = frame.GetProperty("line").GetInt32();
                        csVm.Frames.Add(new StackFrameViewModel(name, $"{Path.GetFileName(source ?? "")}:{line}"));
                    }
                }
            });

            // 3. Populate Locals panel
            var scopesEl = await DebuggerService.Instance.ScopesAsync(topFrameId);
            if (version != _stopVersion || !IsPaused) return;
            if (scopesEl.ValueKind == System.Text.Json.JsonValueKind.Undefined) return;

            var localScope = scopesEl.GetProperty("scopes").EnumerateArray()
                .FirstOrDefault(s => s.GetProperty("name").GetString() == "Locals");
            if (localScope.ValueKind == System.Text.Json.JsonValueKind.Undefined) return;

            var varRef = localScope.GetProperty("variablesReference").GetInt32();
            var varsEl = await DebuggerService.Instance.VariablesAsync(varRef);
            if (version != _stopVersion || !IsPaused) return;
            if (varsEl.ValueKind == System.Text.Json.JsonValueKind.Undefined) return;

            Dispatcher.UIThread.Post(() =>
            {
                if (version != _stopVersion || !IsPaused) return;
                var locVm = LocalsViewModel.Current;
                if (locVm == null) return;
                locVm.Variables.Clear();
                foreach (var v in varsEl.GetProperty("variables").EnumerateArray())
                {
                    var vName  = v.GetProperty("name").GetString()  ?? "";
                    var vValue = v.GetProperty("value").GetString() ?? "";
                    var vType  = v.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                    locVm.Variables.Add(new VariableViewModel(vName, vValue, vType));
                }
            });
        }
        catch (Exception ex)
        {
            BuildOutputService.Instance.AppendLine($"[Debugger] UI refresh error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tells the active editor to highlight the execution line.
    /// </summary>
    private static void NotifyEditorExecutionLine(string filePath, int line)
    {
        try
        {
            if (!string.IsNullOrEmpty(filePath) && line > 0)
            {
                // Open the file in Shell if not already open
                MainWindowViewModel.Instance.Open(filePath);

                // Find all open views and call SetCurrentExecutionLine
                foreach (var entry in MainWindowViewModel.Instance.Views)
                {
                    if (entry.Value is DocumentView view)
                    {
                        bool matches = string.Equals(view.Document?.FilePath, filePath,
                            StringComparison.OrdinalIgnoreCase);
                        view.SetCurrentExecutionLine(matches ? line : -1);
                    }
                }
            }
            else
            {
                // Clear all execution lines
                foreach (var entry in MainWindowViewModel.Instance.Views)
                {
                    var view = entry.Value;
                    var setLine = view?.GetType().GetMethod("SetCurrentExecutionLine");
                    setLine?.Invoke(view, new object[] { -1 });
                }
            }
        }
        catch { /* non-fatal */ }
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private Task Continue() => DebuggerService.Instance.ContinueAsync();
    private bool CanContinue() => IsDebugging && IsPaused;

    [RelayCommand(CanExecute = nameof(CanStep))]
    private Task StepOver() => DebuggerService.Instance.StepOverAsync();
    private bool CanStep() => IsDebugging && IsPaused;

    [RelayCommand(CanExecute = nameof(CanStep))]
    private Task StepInto() => DebuggerService.Instance.StepInAsync();

    [RelayCommand(CanExecute = nameof(CanStep))]
    private Task StepOut() => DebuggerService.Instance.StepOutAsync();

    [RelayCommand(CanExecute = nameof(IsDebugging))]
    private void Stop() => DebuggerService.Instance.StopDebugging();

    partial void OnIsDebuggingChanged(bool value)
    {
        ContinueCommand.NotifyCanExecuteChanged();
        StepOverCommand.NotifyCanExecuteChanged();
        StepIntoCommand.NotifyCanExecuteChanged();
        StepOutCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPausedChanged(bool value)
    {
        ContinueCommand.NotifyCanExecuteChanged();
        StepOverCommand.NotifyCanExecuteChanged();
        StepIntoCommand.NotifyCanExecuteChanged();
        StepOutCommand.NotifyCanExecuteChanged();
    }
}
