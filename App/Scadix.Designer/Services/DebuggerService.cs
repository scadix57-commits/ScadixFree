using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Scadix.Designer.Services;

/// <summary>
/// DAP client for netcoredbg.
///
/// Uses manual Content-Length framing because netcoredbg sends events
/// BEFORE responses (e.g. "capabilities" event before "initialize" response),
/// which breaks StreamJsonRpc's request/response matching.
///
/// Correct handshake (confirmed by raw test):
///   Client → initialize
///   Server ← event: capabilities
///   Server ← response: initialize
///   Server ← event: initialized   ← fires immediately after initialize (not after launch!)
///   Client → setBreakpoints
///   Client → launch
///   Client → configurationDone
/// </summary>
public class DebuggerService : IDisposable
{
    private static readonly Lazy<DebuggerService> _instance = new(() => new DebuggerService());
    public static DebuggerService Instance => _instance.Value;

    private Process?  _dbgProcess;
    private Stream?   _stdin;
    private Stream?   _stdout;
    private int       _seq;
    private bool      _isDebugging;
    private int       _activeThreadId = -1;

    // seq → pending response TCS
    private readonly Dictionary<int, TaskCompletionSource<JsonObject>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private CancellationTokenSource? _readCts;

    // Fires when "initialized" event arrives
    private TaskCompletionSource<bool>? _initializedTcs;

    public event EventHandler?                   DebuggingStarted;
    public event EventHandler?                   DebuggingStopped;
    public event EventHandler?                   Continued;
    public event EventHandler<string>?           OutputReceived;
    public event EventHandler<StoppedEventArgs>? Stopped;

    public bool IsDebugging => _isDebugging;

    private DebuggerService()
    {
        // When a breakpoint is added/removed while debugging, sync immediately
        BreakpointService.Instance.BreakpointsChanged += async (_, e) =>
        {
            if (!_isDebugging) return;
            try
            {
                var sourcePath = e.FilePath;
                if (sourcePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
                    sourcePath.EndsWith(".xaml",  StringComparison.OrdinalIgnoreCase))
                {
                    var cs = sourcePath + ".cs";
                    if (!File.Exists(cs)) return;
                    sourcePath = cs;
                }

                var bpArr = new JsonArray();
                foreach (var ln in e.Breakpoints)
                    bpArr.Add(new JsonObject { ["line"] = ln });

                await SendAsync("setBreakpoints", new JsonObject
                {
                    ["source"]      = new JsonObject { ["path"] = sourcePath, ["name"] = Path.GetFileName(sourcePath) },
                    ["breakpoints"] = bpArr
                });

                BuildOutputService.Instance.AppendLine(
                    $"[Debugger] Live sync: {e.Breakpoints.Count} breakpoint(s) in {Path.GetFileName(sourcePath)}");
            }
            catch { /* non-fatal */ }
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // Start
    // ─────────────────────────────────────────────────────────────────────

    public async Task StartDebuggingAsync(string projectPath, string targetExe)
    {
        if (_isDebugging) return;

        // تحقق من الإعدادات أولاً
        var settings = Settings.Default;
        if (!settings.DebuggerUseNetCoreDbg)
        {
            BuildOutputService.Instance.AppendLine(
                "[Debugger] netcoredbg is not enabled.\n" +
                "  Go to Settings > Build, Execution, Deployment > Debugger\n" +
                "  and enable 'Use netcoredbg'.");
            return;
        }

        string? dbgPath = FindDebuggerPath();
        if (dbgPath == null)
        {
            BuildOutputService.Instance.AppendLine(
                "[Debugger] netcoredbg not found.\n" +
                "  Download: https://github.com/Samsung/netcoredbg/releases\n" +
                "  Then set the path in Settings > Build, Execution, Deployment > Debugger.");
            return;
        }

        try
        {
            BuildOutputService.Instance.AppendLine($"[Debugger] Starting {Path.GetFileName(dbgPath)}");
            BuildOutputService.Instance.AppendLine($"[Debugger] Target:   {targetExe}");

            _dbgProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = dbgPath,
                    Arguments              = "--interpreter=vscode",
                    RedirectStandardInput  = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };

            _dbgProcess.Start();

            // Use BaseStream directly — avoids any encoding/BOM issues
            _stdin  = _dbgProcess.StandardInput.BaseStream;
            _stdout = _dbgProcess.StandardOutput.BaseStream;

            _ = Task.Run(async () =>
            {
                try
                {
                    string? line;
                    while ((line = await _dbgProcess.StandardError.ReadLineAsync()) != null)
                        BuildOutputService.Instance.AppendLine($"[dbg] {line}");
                }
                catch { }
            });

            _isDebugging    = true;
            _initializedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _readCts        = new CancellationTokenSource();
            _ = Task.Run(() => ReadLoopAsync(_readCts.Token));

            DebuggingStarted?.Invoke(this, EventArgs.Empty);

            // ── DAP handshake ─────────────────────────────────────────────
            await SendAsync("initialize", new JsonObject
            {
                ["clientID"]        = "Scadix.Designer",
                ["adapterID"]       = "netcoredbg",
                ["linesStartAt1"]   = true,
                ["columnsStartAt1"] = true,
                ["pathFormat"]      = "path",
                ["supportsRunInTerminalRequest"] = false
            });

            // Wait for "initialized" event
            using var initCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            initCts.Token.Register(() => _initializedTcs?.TrySetCanceled());
            await _initializedTcs.Task;

            // Send breakpoints
            await SyncBreakpointsAsync();

            // Launch — for .dll files netcoredbg needs dotnet as the host
            var isDll = targetExe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            await SendAsync("launch", new JsonObject
            {
                ["name"]        = ".NET Launch",
                ["type"]        = "coreclr",
                ["request"]     = "launch",
                ["program"]     = isDll ? FindDotnetPath() : targetExe,
                ["args"]        = isDll ? new JsonArray { targetExe } : new JsonArray(),
                ["cwd"]         = Path.GetDirectoryName(targetExe) ?? projectPath,
                ["stopAtEntry"] = false,
                ["justMyCode"]  = settings.DebuggerStepOverSystemCode
            });

            // configurationDone → process starts running
            await SendAsync("configurationDone", new JsonObject());

            BuildOutputService.Instance.AppendLine("[Debugger] Running…");
        }
        catch (OperationCanceledException)
        {
            BuildOutputService.Instance.AppendLine("[Debugger] Timed out waiting for adapter.");
            StopDebugging();
        }
        catch (Exception ex)
        {
            BuildOutputService.Instance.AppendLine($"[Debugger] Error: {ex.Message}");
            StopDebugging();
        }
    }

    private static string FindDotnetPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
            @"C:\Program Files\dotnet\dotnet.exe",
            "/usr/bin/dotnet",
            "/usr/local/bin/dotnet"
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try
            {
                var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
                var f = Path.Combine(dir, name);
                if (File.Exists(f)) return f;
            }
            catch { }
        }
        return "dotnet";
    }

    // ─────────────────────────────────────────────────────────────────────
    // Stop
    // ─────────────────────────────────────────────────────────────────────

    public void StopDebugging()
    {
        if (!_isDebugging) return;
        _isDebugging = false;

        // أخذ نسخ محلية قبل المسح
        var cts       = _readCts;
        var initTcs   = _initializedTcs;
        var stdin     = _stdin;
        var process   = _dbgProcess;

        // مسح الحالة فوراً على الـ calling thread
        _readCts        = null;
        _initializedTcs = null;
        _stdin          = null;
        _stdout         = null;
        _dbgProcess     = null;
        _activeThreadId = -1;

        // إلغاء الـ pending requests فوراً
        lock (_pending)
        {
            foreach (var tcs in _pending.Values) tcs.TrySetCanceled();
            _pending.Clear();
        }

        // إطلاق الـ event على الـ UI thread قبل العمليات الثقيلة
        DebuggingStopped?.Invoke(this, EventArgs.Empty);

        // العمليات الثقيلة (Kill, Close) على background thread لمنع تجميد الـ UI
        Task.Run(() =>
        {
            try { cts?.Cancel(); }         catch { }
            try { cts?.Dispose(); }        catch { }
            try { initTcs?.TrySetCanceled(); } catch { }
            try { stdin?.Close(); }        catch { }
            try { stdin?.Dispose(); }      catch { }

            // انتظر قليلاً لإعطاء الـ process فرصة للخروج بنفسه
            try
            {
                if (process != null && !process.HasExited)
                {
                    // أرسل disconnect request أولاً (graceful)
                    process.WaitForExit(1000);
                }
            }
            catch { }

            // إذا لم يخرج، اقتله
            try
            {
                if (process != null && !process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { }

            try { process?.Dispose(); } catch { }
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // Breakpoints
    // ─────────────────────────────────────────────────────────────────────

    public async Task SyncBreakpointsAsync()
    {
        if (!_isDebugging) return;

        var all = BreakpointService.Instance.GetAllBreakpoints();

        BuildOutputService.Instance.AppendLine(
            $"[Debugger] SyncBreakpoints: {all.Count} file(s) with breakpoints.");

        if (all.Count == 0)
        {
            BuildOutputService.Instance.AppendLine("[Debugger] No breakpoints to sync.");
            return;
        }

        foreach (var kvp in all)
        {
            var sourcePath = kvp.Key;
            if (sourcePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
                sourcePath.EndsWith(".xaml",  StringComparison.OrdinalIgnoreCase))
            {
                var csBehind = sourcePath + ".cs";
                if (File.Exists(csBehind))
                    sourcePath = csBehind;
                else
                {
                    BuildOutputService.Instance.AppendLine(
                        $"[Debugger] Skipping breakpoints in '{Path.GetFileName(sourcePath)}' — set breakpoints in .cs files.");
                    continue;
                }
            }

            var lines = string.Join(", ", kvp.Value);
            BuildOutputService.Instance.AppendLine(
                $"[Debugger] Setting breakpoints in {Path.GetFileName(sourcePath)} at line(s): {lines}");

            var bpArr = new JsonArray();
            foreach (var ln in kvp.Value)
                bpArr.Add(new JsonObject { ["line"] = ln });

            var resp = await SendAsync("setBreakpoints", new JsonObject
            {
                ["source"]      = new JsonObject { ["path"] = sourcePath, ["name"] = Path.GetFileName(sourcePath) },
                ["breakpoints"] = bpArr
            });

            if (resp?["body"]?["breakpoints"] is JsonArray verified)
            {
                foreach (var bp in verified)
                {
                    var ok      = bp?["verified"]?.GetValue<bool>() ?? false;
                    var bpLine  = bp?["line"]?.GetValue<int>() ?? 0;
                    var msg     = bp?["message"]?.GetValue<string>() ?? "";
                    BuildOutputService.Instance.AppendLine(
                        ok ? $"[Debugger] ✓ Breakpoint verified at line {bpLine}"
                           : $"[Debugger] ✗ Breakpoint NOT verified at line {bpLine}: {msg}");
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Step / Continue
    // ─────────────────────────────────────────────────────────────────────

    public Task ContinueAsync()  => SendAsync("continue", new JsonObject { ["threadId"] = ActiveThread });
    public Task StepOverAsync()  => SendAsync("next",     new JsonObject { ["threadId"] = ActiveThread });
    public Task StepInAsync()    => SendAsync("stepIn",   new JsonObject { ["threadId"] = ActiveThread });
    public Task StepOutAsync()   => SendAsync("stepOut",  new JsonObject { ["threadId"] = ActiveThread });
    public async Task PauseAsync()
    {
        if (!_isDebugging) return;
        var response = await SendAsync("pause", new JsonObject { ["threadId"] = ActiveThread });
        if (response?["success"]?.GetValue<bool>() != true && _isDebugging)
            BuildOutputService.Instance.AppendLine("[Debugger] Pause failed: " +
                (response?["message"]?.GetValue<string>() ?? "No response from adapter."));
    }

    private int ActiveThread => _activeThreadId > 0 ? _activeThreadId : 1;

    // ─────────────────────────────────────────────────────────────────────
    // Stack / Scopes / Variables / Evaluate
    // ─────────────────────────────────────────────────────────────────────

    public async Task<JsonElement> StackTraceAsync(int threadId)
    {
        var r = await SendAsync("stackTrace", new JsonObject { ["threadId"] = threadId, ["startFrame"] = 0, ["levels"] = 20 });
        return r?["body"]?.Deserialize<JsonElement>() ?? default;
    }

    public async Task<JsonElement> ScopesAsync(int frameId)
    {
        var r = await SendAsync("scopes", new JsonObject { ["frameId"] = frameId });
        return r?["body"]?.Deserialize<JsonElement>() ?? default;
    }

    public async Task<JsonElement> VariablesAsync(int variablesReference)
    {
        var r = await SendAsync("variables", new JsonObject { ["variablesReference"] = variablesReference });
        return r?["body"]?.Deserialize<JsonElement>() ?? default;
    }

    public async Task<string> EvaluateAsync(string expression, int frameId)
    {
        if (!_isDebugging) return "Not debugging";
        try
        {
            var r = await SendAsync("evaluate", new JsonObject
                { ["expression"] = expression, ["frameId"] = frameId, ["context"] = "watch" });
            return r?["body"]?["result"]?.GetValue<string>() ?? "";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Transport: write
    // ─────────────────────────────────────────────────────────────────────

    private async Task<JsonObject?> SendAsync(string command, JsonObject args, int timeoutMs = 10_000)
    {
        var stdin = _stdin;
        if (stdin == null) return null;

        int seq = Interlocked.Increment(ref _seq);
        var tcs = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pending) _pending[seq] = tcs;

        var msg  = new JsonObject { ["seq"] = seq, ["type"] = "request", ["command"] = command, ["arguments"] = args };
        var body = Encoding.UTF8.GetBytes(msg.ToJsonString());
        var hdr  = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");

        await _writeLock.WaitAsync();
        try
        {
            if (_stdin == null) return null;   // تحقق مرة أخرى بعد الانتظار
            await stdin.WriteAsync(hdr);
            await stdin.WriteAsync(body);
            await stdin.FlushAsync();
        }
        catch (ObjectDisposedException) { return null; }
        catch (IOException)             { return null; }
        finally { _writeLock.Release(); }

        using var cts = new CancellationTokenSource(timeoutMs);
        cts.Token.Register(() => tcs.TrySetCanceled());
        try   { return await tcs.Task; }
        catch { lock (_pending) _pending.Remove(seq); return null; }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Transport: read loop
    // ─────────────────────────────────────────────────────────────────────

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stdout != null)
            {
                var msg = await ReadMessageAsync(ct);
                if (msg == null) break;
                Dispatch(msg);
            }
        }
        catch (OperationCanceledException) { /* طبيعي عند الإيقاف */ }
        catch (ObjectDisposedException)    { /* طبيعي عند إغلاق الـ stream */ }
        catch (IOException)                { /* طبيعي عند إغلاق الـ process */ }
        catch (Exception ex)
        {
            if (_isDebugging)
                BuildOutputService.Instance.AppendLine($"[Debugger] Read error: {ex.Message}");
        }
        finally
        {
            // استدعِ StopDebugging فقط إذا كان لا يزال يعمل
            // (لم يُستدعَ StopDebugging من الخارج بالفعل)
            if (_isDebugging)
                StopDebugging();
        }
    }

    private async Task<JsonObject?> ReadMessageAsync(CancellationToken ct)
    {
        int contentLength = 0;
        var lineBuf = new List<byte>(128);

        while (true)
        {
            lineBuf.Clear();
            while (true)
            {
                int b = await ReadByteAsync(ct);
                if (b < 0) return null;
                if (b == '\n') break;
                if (b != '\r') lineBuf.Add((byte)b);
            }
            var line = Encoding.ASCII.GetString(lineBuf.ToArray());
            if (line.Length == 0) break;
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                contentLength = int.Parse(line.Substring(15).Trim());
        }

        if (contentLength <= 0) return null;

        var body = new byte[contentLength];
        int read = 0;
        while (read < contentLength)
        {
            var stdout = _stdout;
            if (stdout == null) return null;
            int n = await stdout.ReadAsync(body, read, contentLength - read, ct);
            if (n == 0) return null;
            read += n;
        }

        return JsonNode.Parse(body) as JsonObject;
    }

    private async Task<int> ReadByteAsync(CancellationToken ct)
    {
        var stdout = _stdout;
        if (stdout == null) return -1;
        var buf = new byte[1];
        try
        {
            return await stdout.ReadAsync(buf, 0, 1, ct) == 0 ? -1 : buf[0];
        }
        catch (ObjectDisposedException) { return -1; }
        catch (IOException)             { return -1; }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Dispatch incoming messages
    // ─────────────────────────────────────────────────────────────────────

    private void Dispatch(JsonObject msg)
    {
        var type = msg["type"]?.GetValue<string>();

        if (type == "response")
        {
            // Execution state comes from adapter events. A resume response may arrive
            // after a newer stopped event and must not clear that newer pause.
            var reqSeq = msg["request_seq"]?.GetValue<int>() ?? -1;
            TaskCompletionSource<JsonObject>? tcs;
            lock (_pending) { _pending.TryGetValue(reqSeq, out tcs); _pending.Remove(reqSeq); }
            tcs?.TrySetResult(msg);
        }
        else if (type == "event")
        {
            HandleEvent(msg["event"]?.GetValue<string>(), msg["body"] as JsonObject);
        }
    }

    private void HandleEvent(string? evt, JsonObject? body)
    {
        switch (evt)
        {
            case "continued":
                Continued?.Invoke(this, EventArgs.Empty);
                break;
            case "initialized":
                BuildOutputService.Instance.AppendLine("[Debugger] Adapter initialized.");
                _initializedTcs?.TrySetResult(true);
                break;

            case "stopped":
                var reason   = body?["reason"]?.GetValue<string>()     ?? "unknown";
                var threadId = body?["threadId"]?.GetValue<int>()       ?? 1;
                var desc     = body?["description"]?.GetValue<string>() ?? "";
                var text     = body?["text"]?.GetValue<string>()        ?? "";
                _activeThreadId = threadId;
                BuildOutputService.Instance.AppendLine(
                    $"[Debugger] ⏸ Stopped: {reason}" +
                    (string.IsNullOrEmpty(desc) ? "" : $" — {desc}") +
                    (string.IsNullOrEmpty(text) ? "" : $" ({text})") +
                    $" [thread {threadId}]");
                Stopped?.Invoke(this, new StoppedEventArgs(reason, threadId));
                break;

            case "output":
                var cat  = body?["category"]?.GetValue<string>() ?? "stdout";
                var out_ = body?["output"]?.GetValue<string>()   ?? "";
                if (cat != "telemetry")
                {
                    var line = $"[{cat}] {out_.TrimEnd()}";
                    OutputReceived?.Invoke(this, line);
                    BuildOutputService.Instance.AppendLine(line);
                }
                break;

            case "terminated":
            case "exited":
                var code = body?["exitCode"]?.GetValue<int>() ?? -1;
                BuildOutputService.Instance.AppendLine($"[Debugger] Process exited (code {code}).");
                StopDebugging();
                break;

            case "thread":
                var tid = body?["threadId"]?.GetValue<int>() ?? -1;
                var r2  = body?["reason"]?.GetValue<string>() ?? "";
                if (r2 == "started" && _activeThreadId < 0)
                    _activeThreadId = tid;
                else if (r2 == "exited" && _activeThreadId == tid)
                    _activeThreadId = -1;
                BuildOutputService.Instance.AppendLine($"[Debugger] Thread {tid}: {r2}");
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Find netcoredbg — يقرأ المسار من الإعدادات أولاً
    // ─────────────────────────────────────────────────────────────────────

    private static string? FindDebuggerPath()
    {
        var name = OperatingSystem.IsWindows() ? "netcoredbg.exe" : "netcoredbg";

        // 1. المسار المحدد في الإعدادات
        var settingsPath = Settings.Default.DebuggerNetCoreDbgPath;
        if (!string.IsNullOrWhiteSpace(settingsPath))
        {
            // إذا كان مساراً كاملاً وملف موجود
            if (File.Exists(settingsPath)) return settingsPath;

            // إذا كان اسماً فقط (مثل "netcoredbg") ابحث في PATH
            if (!settingsPath.Contains(Path.DirectorySeparatorChar) &&
                !settingsPath.Contains(Path.AltDirectorySeparatorChar))
            {
                var fromPath = FindInPath(settingsPath);
                if (fromPath != null) return fromPath;
            }
        }

        // 2. بجانب الـ IDE مباشرة
        var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
        if (File.Exists(local)) return local;

        // 3. في مجلد فرعي netcoredbg بجانب الـ IDE
        var sub = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "netcoredbg", name);
        if (File.Exists(sub)) return sub;

        // 4. في PATH
        return FindInPath(name);
    }

    private static string? FindInPath(string name)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try
            {
                var f = Path.Combine(dir, name);
                if (File.Exists(f)) return f;
            }
            catch { }
        }
        return null;
    }

    public void Dispose() => StopDebugging();
}

public class StoppedEventArgs : EventArgs
{
    public string Reason   { get; }
    public int    ThreadId { get; }
    public StoppedEventArgs(string reason, int threadId) { Reason = reason; ThreadId = threadId; }
}
