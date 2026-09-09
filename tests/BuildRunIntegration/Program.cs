using Scadix.Designer.Services;
using System.Collections.Concurrent;

var root = Path.Combine(Path.GetTempPath(), "Scadix Build Run " + Guid.NewGuid());
Directory.CreateDirectory(root);
var logs = new ConcurrentQueue<string>();
var runner = new BuildRunService();
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
try
{
    var target = Path.Combine(root, "Chosen.proj");
    await File.WriteAllTextAsync(target, "<Project><Target Name=\"Build\"><Message Text=\"CHOSEN_TARGET\" Importance=\"high\" /></Target></Project>");
    await File.WriteAllTextAsync(Path.Combine(root, "Other.proj"), "<Project />");
    var result = await runner.ExecuteAsync("build", target, "Debug", null, logs.Enqueue);
    Check(result == BuildRunResult.Succeeded && logs.Any(x => x.Contains("CHOSEN_TARGET")), "Build chooses explicit target in directory with spaces and multiple projects");
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var pending = runner.ExecuteAsync("build", target, "Debug", null, logs.Enqueue, async token => { entered.SetResult(); return await release.Task.WaitAsync(token); });
    await entered.Task;
    Check(await runner.ExecuteAsync("build", target, "Debug", null, logs.Enqueue) == BuildRunResult.Busy, "Duplicate operation rejected while saving");
    runner.Cancel();
    Check(await pending == BuildRunResult.Cancelled, "Cancel during save prevents launch");
    Check(await runner.ExecuteAsync("build", target, "Debug", null, logs.Enqueue, _ => Task.FromResult(false)) == BuildRunResult.Cancelled, "Save cancellation prevents launch");
    var project = Path.Combine(root, "Chosen.csproj");
    await File.WriteAllTextAsync(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
    await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), "System.Console.WriteLine(\"RUN_CHOSEN\"); System.Console.WriteLine(System.Environment.ProcessId); await System.Threading.Tasks.Task.Delay(60000);");
    var running = runner.ExecuteAsync("run", project, "Debug", "net10.0", logs.Enqueue);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    while (!logs.Contains("RUN_CHOSEN")) { await Task.Delay(50, timeout.Token); if (running.IsCompleted) throw new Exception(string.Join("\n", logs)); }
    Check(await runner.ExecuteAsync("clean", project, "Debug", null, logs.Enqueue) == BuildRunResult.Busy, "Duplicate rejected while process runs");
    runner.Cancel();
    Check(await running.WaitAsync(TimeSpan.FromSeconds(10)) == BuildRunResult.Cancelled, "Run uses --project and cancels promptly");
    var pid = logs.Select(x => int.TryParse(x, out var n) ? n : 0).Last(x => x > 0);
    bool exited;
    try { using var child = System.Diagnostics.Process.GetProcessById(pid); exited = child.HasExited; } catch (ArgumentException) { exited = true; }
    Check(exited, "Cancellation terminates target child process");
    Check(await runner.ExecuteAsync("build", target, "Debug", null, logs.Enqueue) == BuildRunResult.Succeeded, "Can build again after cancellation");
    await File.WriteAllTextAsync(target, "<Project><Target Name=\"Build\"><Error Text=\"EXPECTED_FAILURE\" /></Target></Project>");
    Check(await runner.ExecuteAsync("build", target, "Debug", null, logs.Enqueue) == BuildRunResult.Failed, "Nonzero exit reported as failure");
    Console.WriteLine("TOTAL FAILURES: 0");
}
finally
{
    runner.Cancel();
    Directory.Delete(root, true);
}
