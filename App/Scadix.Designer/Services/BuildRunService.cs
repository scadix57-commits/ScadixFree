using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Scadix.Designer.Services;

public enum BuildRunResult { Succeeded, Failed, Cancelled, Busy }

/// <summary>Owns one dotnet operation, including preparation and its process tree.</summary>
public sealed class BuildRunService
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;

    public void Cancel()
    {
        lock (_gate) _cancellation?.Cancel();
    }

    public async Task<BuildRunResult> ExecuteAsync(string operation, string targetPath,
        string configuration, string? framework, Action<string> output,
        Func<CancellationToken, Task<bool>>? prepare = null)
    {
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_cancellation != null) return BuildRunResult.Busy;
            _cancellation = cancellation = new CancellationTokenSource();
        }
        var token = cancellation.Token;
        try
        {
            if (operation is not ("build" or "build --no-incremental" or "clean" or "run"))
                throw new ArgumentException("Unsupported dotnet operation.");
            targetPath = Path.GetFullPath(targetPath);
            if (!File.Exists(targetPath)) throw new FileNotFoundException("Target does not exist.", targetPath);
            if (prepare != null && !await prepare(token)) return BuildRunResult.Cancelled;
            token.ThrowIfCancellationRequested();

            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Path.GetDirectoryName(targetPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(operation == "build --no-incremental" ? "build" : operation);
            if (operation == "run") start.ArgumentList.Add("--project");
            start.ArgumentList.Add(targetPath);
            start.ArgumentList.Add("--configuration");
            start.ArgumentList.Add(configuration);
            if (operation == "build --no-incremental") start.ArgumentList.Add("--no-incremental");
            if (operation == "run" && !string.IsNullOrWhiteSpace(framework))
            {
                start.ArgumentList.Add("--framework");
                start.ArgumentList.Add(framework);
            }
            output($"Target: {targetPath}");
            output($"> dotnet {string.Join(" ", start.ArgumentList)}");
            using var process = new Process { StartInfo = start };
            process.Start();
            using var registration = token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* Process already exited. */ }
            });
            async Task DrainAsync(StreamReader reader)
            {
                while (await reader.ReadLineAsync() is { } line) output(line);
            }
            await Task.WhenAll(DrainAsync(process.StandardOutput), DrainAsync(process.StandardError), process.WaitForExitAsync());
            if (token.IsCancellationRequested) return BuildRunResult.Cancelled;
            output($"Process finished with exit code {process.ExitCode}");
            return process.ExitCode == 0 ? BuildRunResult.Succeeded : BuildRunResult.Failed;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return BuildRunResult.Cancelled;
        }
        catch (Exception ex)
        {
            output($"Error: {ex.Message}");
            return BuildRunResult.Failed;
        }
        finally
        {
            lock (_gate)
            {
                _cancellation = null;
                cancellation.Dispose();
            }
        }
    }
}
