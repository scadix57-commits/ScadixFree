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
using Scadix.Designer.Services;

namespace Scadix.Designer.ViewModels.Tools;

// ── Console Output Line ───────────────────────────────────────────────────────
public enum PmcLineKind { Command, Output, Error, Success, Info }

public class PmcLine
{
    public string     Text { get; }
    public PmcLineKind Kind { get; }
    public DateTime   Time { get; } = DateTime.Now;

    public PmcLine(string text, PmcLineKind kind) { Text = text; Kind = kind; }
}

// ── PackageManagerConsoleViewModel ────────────────────────────────────────────
/// <summary>
/// A persistent-process NuGet/dotnet CLI console.
/// Philosophy: stateless process-per-command (unlike Terminal which holds a PTY).
/// Each command spawns a short-lived dotnet process, streams output in real-time,
/// then terminates cleanly. This gives us reliable exit codes and avoids shell
/// contamination while still feeling interactive.
/// </summary>
public partial class PackageManagerConsoleViewModel : Tool
{
    public static PackageManagerConsoleViewModel? Current { get; private set; }

    // ── Observable State ──────────────────────────────────────────────────────

    [ObservableProperty] private string _input = "";
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _targetProject = "";

    // Ring-buffer: max 1000 lines to avoid memory growth
    private const int MaxLines = 1000;
    public ObservableCollection<PmcLine> Output { get; } = new();

    // Command history (per-session)
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    // Currently-running command cancellation
    private CancellationTokenSource? _runCts;

    // Predefined quick-access commands shown as chips
    public static readonly string[] QuickCommands =
    {
        "list",
        "restore",
        "outdated",
    };

    public PackageManagerConsoleViewModel()
    {
        Id       = "PackageConsole";
        Title    = "Package Manager Console";
        CanClose = true;
        Current  = this;

        RefreshTargetProject();
        Append("Package Manager Console — type 'help' for a command reference.", PmcLineKind.Info);
        Append("Project: " + (_targetProject.Length > 0 ? _targetProject : "(none loaded)"), PmcLineKind.Info);
    }

    // ── Command Execution ─────────────────────────────────────────────────────

    [RelayCommand]
    public async Task RunInputAsync()
    {
        var raw = Input.Trim();
        if (string.IsNullOrEmpty(raw)) return;

        Input = "";
        if (_history.Count == 0 || _history[^1] != raw)
            _history.Add(raw);
        _historyIndex = _history.Count;

        await ExecuteAsync(raw);
    }

    [RelayCommand]
    public async Task RunQuickAsync(string shorthand)
    {
        // Map shorthand to full dotnet nuget / dotnet list commands
        var cmd = shorthand switch
        {
            "list"     => "list package",
            "restore"  => "restore",
            "outdated" => "list package --outdated",
            _          => shorthand
        };
        await ExecuteAsync(cmd);
    }

    [RelayCommand]
    public void Cancel()
    {
        _runCts?.Cancel();
    }

    [RelayCommand]
    public void ClearOutput() => Dispatcher.UIThread.Post(Output.Clear);

    // ── History navigation ────────────────────────────────────────────────────

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

    // ── Core runner ───────────────────────────────────────────────────────────

    private async Task ExecuteAsync(string userInput)
    {
        if (IsBusy)
        {
            Append("A command is already running. Use Cancel to stop it.", PmcLineKind.Error);
            return;
        }

        // Parse built-in help
        if (userInput.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            ShowHelp();
            return;
        }

        if (userInput.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            ClearOutput();
            return;
        }

        Append($"> {userInput}", PmcLineKind.Command);

        // Build the dotnet command
        var (exe, args, workDir) = BuildCommand(userInput);
        if (exe == null)
        {
            Append("Unknown command. Type 'help' for available commands.", PmcLineKind.Error);
            return;
        }

        IsBusy  = true;
        _runCts = new CancellationTokenSource();
        var ct  = _runCts.Token;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = exe,
                Arguments              = args,
                WorkingDirectory       = workDir,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8,
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.Start();

            // Stream stdout and stderr concurrently
            var stdoutTask = ReadStreamAsync(proc.StandardOutput, PmcLineKind.Output, ct);
            var stderrTask = ReadStreamAsync(proc.StandardError,  PmcLineKind.Error,  ct);

            await Task.WhenAll(
                proc.WaitForExitAsync(ct),
                stdoutTask,
                stderrTask
            );

            var exitCode = proc.ExitCode;
            Append(
                exitCode == 0 ? "✓ Command completed successfully." : $"✗ Exited with code {exitCode}.",
                exitCode == 0 ? PmcLineKind.Success : PmcLineKind.Error
            );
        }
        catch (OperationCanceledException)
        {
            Append("Command cancelled.", PmcLineKind.Info);
        }
        catch (Exception ex)
        {
            Append($"Error: {ex.Message}", PmcLineKind.Error);
        }
        finally
        {
            IsBusy = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    private async Task ReadStreamAsync(StreamReader reader, PmcLineKind kind, CancellationToken ct)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                var captured = line;
                await Dispatcher.UIThread.InvokeAsync(() => Append(captured, kind));
            }
        }
        catch (OperationCanceledException) { }
    }

    // ── Command parser ────────────────────────────────────────────────────────

    private (string? exe, string args, string workDir) BuildCommand(string input)
    {
        var workDir = GetWorkDir();
        var tokens  = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var verb    = tokens[0].ToLowerInvariant();
        var rest    = tokens.Length > 1 ? tokens[1] : "";

        var (exe, args) = verb switch
        {
            // NuGet operations mapped to dotnet CLI
            "install"   => ("dotnet", $"add \"{_targetProject}\" package {rest}"),
            "uninstall" => ("dotnet", $"remove \"{_targetProject}\" package {rest}"),
            "update"    => ("dotnet", $"add \"{_targetProject}\" package {rest}"),
            "restore"   => ("dotnet", $"restore \"{_targetProject}\""),
            "list"      => ("dotnet", $"list \"{_targetProject}\" package {rest}"),

            // dotnet passthrough — user can type any dotnet subcommand
            "dotnet"    => ("dotnet", rest),
            "build"     => ("dotnet", $"build \"{_targetProject}\""),
            "clean"     => ("dotnet", $"clean \"{_targetProject}\""),

            _ => ((string?)null, "")
        };

        return (exe, args, workDir);
    }

    // ── Help ──────────────────────────────────────────────────────────────────

    private void ShowHelp()
    {
        var lines = new[]
        {
            "─── Package Manager Console Commands ────────────────────────────",
            "  install  <PackageId> [version]    Add a NuGet package",
            "  uninstall <PackageId>             Remove a NuGet package",
            "  update   <PackageId>              Update to latest version",
            "  restore                           Restore all packages",
            "  list                              List installed packages",
            "  list --outdated                   Show outdated packages",
            "  build                             Build the startup project",
            "  clean                             Clean build output",
            "  dotnet <args>                     Run any dotnet CLI command",
            "  clear                             Clear console",
            "─────────────────────────────────────────────────────────────────",
        };
        foreach (var l in lines) Append(l, PmcLineKind.Info);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshTargetProject()
    {
        var proj = MainWindowViewModel.Instance?.SelectedStartupProject?.FilePath ?? "";
        _targetProject = proj;
    }

    private string GetWorkDir()
    {
        var fp = MainWindowViewModel.Instance?.SelectedStartupProject?.FilePath ?? "";
        if (!string.IsNullOrEmpty(fp))
        {
            var dir = File.Exists(fp) ? Path.GetDirectoryName(fp) : fp;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private void Append(string text, PmcLineKind kind)
    {
        Dispatcher.UIThread.Post(() =>
        {
            while (Output.Count >= MaxLines)
                Output.RemoveAt(0);
            Output.Add(new PmcLine(text, kind));
        });
    }
}
