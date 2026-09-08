using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Scadix.Designer.Services;

public class ProcessMetrics : ObservableObject
{
    private double _cpuUsage;
    public double CpuUsage
    {
        get => _cpuUsage;
        set => SetProperty(ref _cpuUsage, value);
    }

    private double _memoryUsageMb;
    public double MemoryUsageMb
    {
        get => _memoryUsageMb;
        set => SetProperty(ref _memoryUsageMb, value);
    }

    private double _gcHeapSizeMb;
    public double GcHeapSizeMb
    {
        get => _gcHeapSizeMb;
        set => SetProperty(ref _gcHeapSizeMb, value);
    }
}

public class DiagnosticsService : ObservableObject
{
    private static DiagnosticsService? _instance;
    public static DiagnosticsService Instance => _instance ??= new DiagnosticsService();

    private CancellationTokenSource? _cts;
    private readonly ProcessMetrics _currentMetrics = new();
    public ProcessMetrics CurrentMetrics => _currentMetrics;

    // ── For cross-process monitoring (external process mode) ──────────────
    private Process? _monitoredProcess;

    // ── For in-process monitoring ─────────────────────────────────────────
    private Timer? _inProcessTimer;
    private TimeSpan _lastTotalCpu;
    private DateTime _lastSampleTime;

    private bool _isMonitoring;
    public bool IsMonitoring
    {
        get => _isMonitoring;
        private set => SetProperty(ref _isMonitoring, value);
    }

    private DiagnosticsService() { }

    // ─────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Monitors an EXTERNAL process via EventPipe diagnostics.
    /// </summary>
    public void StartMonitoring(int processId)
    {
        StopMonitoring();

        _cts = new CancellationTokenSource();
        IsMonitoring = true;

        Task.Run(() => MonitorExternalProcessAsync(processId, _cts.Token));
    }

    /// <summary>
    /// Monitors the CURRENT (IDE's own) process — used during In-Process runs.
    /// Polls GC and CPU metrics on a 1-second interval without requiring EventPipe.
    /// </summary>
    public void StartInProcessMonitoring()
    {
        StopMonitoring();
        IsMonitoring = true;

        _lastTotalCpu = Process.GetCurrentProcess().TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;

        _inProcessTimer = new Timer(
            SampleInProcess,
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    public void StopMonitoring()
    {
        // Stop external monitor
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        // Stop in-process timer
        _inProcessTimer?.Dispose();
        _inProcessTimer = null;

        _monitoredProcess = null;
        IsMonitoring = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // In-Process sampling
    // ─────────────────────────────────────────────────────────────────────

    private void SampleInProcess(object? _)
    {
        try
        {
            var proc = Process.GetCurrentProcess();

            // CPU
            var now = DateTime.UtcNow;
            var cpu = proc.TotalProcessorTime;
            double elapsedMs = (now - _lastSampleTime).TotalMilliseconds;
            double cpuMs = (cpu - _lastTotalCpu).TotalMilliseconds;
            double cpuPct = elapsedMs > 0
                ? Math.Round(cpuMs / elapsedMs / Environment.ProcessorCount * 100.0, 1)
                : 0;
            _lastTotalCpu = cpu;
            _lastSampleTime = now;

            // Memory
            proc.Refresh();
            double memMb = Math.Round(proc.WorkingSet64 / 1024.0 / 1024.0, 1);

            // GC heap
            var gcInfo = GC.GetGCMemoryInfo();
            double gcMb = Math.Round(gcInfo.HeapSizeBytes / 1024.0 / 1024.0, 1);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentMetrics.CpuUsage = Math.Clamp(cpuPct, 0, 100);
                CurrentMetrics.MemoryUsageMb = memMb;
                CurrentMetrics.GcHeapSizeMb = gcMb;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiagnosticsService] InProcess sample error: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // External-process monitoring via EventPipe
    // ─────────────────────────────────────────────────────────────────────

    private async Task MonitorExternalProcessAsync(int pid, CancellationToken ct)
    {
        try
        {
            await MonitorWithEventPipeAsync(pid, ct);
        }
        catch (OperationCanceledException) { /* normal */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiagnosticsService] External monitor error: {ex.Message}");
            IsMonitoring = false;
        }
    }

    private async Task MonitorWithEventPipeAsync(int pid, CancellationToken ct)
    {
        try
        {
            // Dynamic load to keep soft dependency
            var clientType = Type.GetType(
                "Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient, Microsoft.Diagnostics.NETCore.Client");
            if (clientType == null)
            {
                // Fallback: poll via Process API
                await PollExternalProcessAsync(pid, ct);
                return;
            }

            var client = Activator.CreateInstance(clientType, pid)!;
            var providers = new List<object>
            {
                CreateEventPipeProvider("System.Runtime", EventLevel.Informational, 0x1,
                    new Dictionary<string, string> { { "EventCounterIntervalSec", "1" } })
            };

            var sessionMethod = clientType.GetMethod("StartEventPipeSession",
                new[] { typeof(IEnumerable<object>), typeof(bool) });

            if (sessionMethod == null)
            {
                await PollExternalProcessAsync(pid, ct);
                return;
            }

            using var session = (IDisposable)sessionMethod.Invoke(client, new object[] { providers, false })!;
        }
        catch
        {
            await PollExternalProcessAsync(pid, ct);
        }
    }

    private object CreateEventPipeProvider(string name, EventLevel level, long keywords,
        Dictionary<string, string> args)
    {
        var providerType = Type.GetType(
            "Microsoft.Diagnostics.NETCore.Client.EventPipeProvider, Microsoft.Diagnostics.NETCore.Client");
        if (providerType == null) return new object();
        return Activator.CreateInstance(providerType, name, level, keywords, args)!;
    }

    /// <summary>
    /// Fallback: poll an external process using System.Diagnostics.Process.
    /// </summary>
    private async Task PollExternalProcessAsync(int pid, CancellationToken ct)
    {
        Process? proc = null;
        TimeSpan lastCpu = TimeSpan.Zero;
        DateTime lastSample = DateTime.UtcNow;

        try { proc = Process.GetProcessById(pid); }
        catch { IsMonitoring = false; return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, ct);
                proc.Refresh();

                if (proc.HasExited) break;

                var now = DateTime.UtcNow;
                var cpu = proc.TotalProcessorTime;
                double elapsedMs = (now - lastSample).TotalMilliseconds;
                double cpuMs = (cpu - lastCpu).TotalMilliseconds;
                double cpuPct = elapsedMs > 0
                    ? Math.Round(cpuMs / elapsedMs / Environment.ProcessorCount * 100.0, 1)
                    : 0;
                lastCpu = cpu;
                lastSample = now;

                double memMb = Math.Round(proc.WorkingSet64 / 1024.0 / 1024.0, 1);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    CurrentMetrics.CpuUsage = Math.Clamp(cpuPct, 0, 100);
                    CurrentMetrics.MemoryUsageMb = memMb;
                });
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiagnosticsService] Poll error: {ex.Message}");
                break;
            }
        }

        IsMonitoring = false;
        proc.Dispose();
    }
}
