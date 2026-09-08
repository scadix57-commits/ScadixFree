using System;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Scadix.Designer.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Scadix.Designer.ViewModels.Tools;

public partial class DiagnosticToolViewModel : Tool
{
    private readonly DiagnosticsService _svc;
    private Timer? _sessionTimer;

    // ── Session ──────────────────────────────────────────────────────────
    [ObservableProperty] private ProcessMetrics _metrics;
    [ObservableProperty] private bool   _isMonitoring;
    [ObservableProperty] private string _sessionTime    = "0 seconds";
    [ObservableProperty] private int    _sessionSeconds;
    [ObservableProperty] private bool   _isRecordingCpu;
    [ObservableProperty] private int    _snapshotCount;
    [ObservableProperty] private int    _eventCount;

    // ── Section collapse state ────────────────────────────────────────────
    [ObservableProperty] private bool _eventsExpanded = true;
    [ObservableProperty] private bool _memoryExpanded = true;
    [ObservableProperty] private bool _cpuExpanded    = true;

    // ── Charts ────────────────────────────────────────────────────────────
    private readonly ObservableCollection<double> _cpuValues    = new();
    private readonly ObservableCollection<double> _memoryValues = new();

    public ISeries[] CpuSeries    { get; set; }
    public ISeries[] MemorySeries { get; set; }
    public Axis[]    XAxes        { get; set; }
    public Axis[]    CpuYAxes     { get; set; }
    public Axis[]    MemoryYAxes  { get; set; }

    // ── Snapshots list ────────────────────────────────────────────────────
    public ObservableCollection<SnapshotEntry> Snapshots { get; } = new();

    public DiagnosticToolViewModel()
    {
        Id    = "Diagnostics";
        Title = "Diagnostics";

        _svc     = DiagnosticsService.Instance;
        _metrics = _svc.CurrentMetrics;

        // Charts
        CpuSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values         = _cpuValues,
                Name           = "CPU %",
                Fill           = new SolidColorPaint(new SKColor(0x00, 0x7A, 0xCC, 50)),
                GeometrySize   = 0,
                Stroke         = new SolidColorPaint(new SKColor(0x00, 0x7A, 0xCC), 2),
                LineSmoothness = 0.5
            }
        };

        MemorySeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values         = _memoryValues,
                Name           = "Private Bytes (MB)",
                Fill           = new SolidColorPaint(new SKColor(0x00, 0x78, 0xD4, 50)),
                GeometrySize   = 0,
                Stroke         = new SolidColorPaint(new SKColor(0x00, 0x78, 0xD4), 2),
                LineSmoothness = 0.5
            }
        };

        XAxes       = new Axis[] { new Axis { IsVisible = false } };
        CpuYAxes    = new Axis[] { new Axis { MinLimit = 0, MaxLimit = 100, Labeler = v => $"{v}%",  TextSize = 10 } };
        MemoryYAxes = new Axis[] { new Axis { MinLimit = 0,                 Labeler = v => $"{v}MB", TextSize = 10 } };

        // Subscribe
        _svc.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DiagnosticsService.IsMonitoring))
                IsMonitoring = _svc.IsMonitoring;
        };

        _svc.CurrentMetrics.PropertyChanged += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (e.PropertyName == nameof(ProcessMetrics.CpuUsage))
                {
                    _cpuValues.Add(_svc.CurrentMetrics.CpuUsage);
                    if (_cpuValues.Count > 120) _cpuValues.RemoveAt(0);
                }
                else if (e.PropertyName == nameof(ProcessMetrics.MemoryUsageMb))
                {
                    _memoryValues.Add(_svc.CurrentMetrics.MemoryUsageMb);
                    if (_memoryValues.Count > 120) _memoryValues.RemoveAt(0);
                }
            });
        };

        IsMonitoring = _svc.IsMonitoring;
    }

    // ── Session timer ─────────────────────────────────────────────────────

    public void StartSession()
    {
        SessionSeconds = 0;
        SessionTime    = "0 seconds";
        _sessionTimer?.Dispose();
        _sessionTimer = new Timer(_ =>
        {
            SessionSeconds++;
            SessionTime = SessionSeconds == 1 ? "1 second"
                                               : $"{SessionSeconds} seconds";
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void StopSession()
    {
        _sessionTimer?.Dispose();
        _sessionTimer  = null;
        IsRecordingCpu = false;
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void TakeSnapshot()
    {
        SnapshotCount++;
        var entry = new SnapshotEntry
        {
            Index     = SnapshotCount,
            TimeLabel = $"{SessionSeconds}s",
            MemoryMb  = _svc.CurrentMetrics.MemoryUsageMb,
            GcHeapMb  = _svc.CurrentMetrics.GcHeapSizeMb
        };
        Snapshots.Insert(0, entry);
    }

    [RelayCommand]
    private void ToggleCpuRecording() => IsRecordingCpu = !IsRecordingCpu;

    [RelayCommand]
    public void Clear()
    {
        _cpuValues.Clear();
        _memoryValues.Clear();
        Snapshots.Clear();
        SnapshotCount = 0;
        EventCount    = 0;
    }

    [RelayCommand] private void ToggleEvents() => EventsExpanded = !EventsExpanded;
    [RelayCommand] private void ToggleMemory() => MemoryExpanded = !MemoryExpanded;
    [RelayCommand] private void ToggleCpu()    => CpuExpanded    = !CpuExpanded;
}

public class SnapshotEntry
{
    public int    Index     { get; set; }
    public string TimeLabel { get; set; } = "";
    public double MemoryMb  { get; set; }
    public double GcHeapMb  { get; set; }
    public string Label     => $"#{Index} @ {TimeLabel}  —  {MemoryMb:N1} MB  /  GC: {GcHeapMb:N1} MB";
}
