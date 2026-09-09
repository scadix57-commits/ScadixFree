using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Scadix.Designer;
using Scadix.Designer.Services;
using Scadix.Designer.ViewModels.Tools;

internal static class Program
{
    static int failures;
    static void Check(bool ok, string label)
    { Console.WriteLine((ok ? "PASS: " : "FAIL: ") + label); if (!ok) failures++; }
    static async Task Until(Func<bool> condition, string label, int seconds = 20)
    {
        var limit = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            if (DateTime.UtcNow > limit) throw new Exception("Timeout: " + label);
            await Task.Delay(50);
        }
    }
    static void Key(Control view, Key key, KeyModifiers mods = KeyModifiers.None) =>
        view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = mods });
    static void Capture(Window window, string name)
    {
        using var bmp = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        bmp.Render(window); bmp.Save("artifacts/DebuggerDeep/" + name + ".png");
    }
    [STAThread]
    static int Main(string[] args)
    {
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        using var stop = new CancellationTokenSource();
        Dispatcher.UIThread.Post(async () =>
        {
            Window? main = null; Window? panelsWindow = null;
            var dbg = DebuggerService.Instance;
            try
            {
                CheckProtocolOrdering(dbg);
                Settings.Default.DebuggerUseNetCoreDbg = true;
                Settings.Default.DebuggerNetCoreDbgPath = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/debugger/netcoredbg/netcoredbg.exe");
                var logs = BuildOutputService.Instance.BuildLogs;
                logs.CollectionChanged += (_, e) => { if (e.NewItems != null) foreach (var item in e.NewItems) Console.WriteLine(item); };
                var vm = DebugToolbarViewModel.Instance;
                int stopCount = 0, thread = 0; var targetIds = new List<int>();
                dbg.Stopped += (_, e) => Dispatcher.UIThread.Post(() => { stopCount++; thread = e.ThreadId; });
                main = new MainWindow(); main.Show();
                var callStack = CallStackViewModel.Current ?? throw new Exception("Call Stack model missing");
                var locals = LocalsViewModel.Current ?? throw new Exception("Locals model missing");
                var locator = new ViewLocator();
                var stackPanel = locator.Build(callStack)!;
                var localsPanel = locator.Build(locals)!;
                Check(stackPanel is not TextBlock && localsPanel is not TextBlock, "Call Stack and Locals resolve to real panels");
                var panels = new Grid { RowDefinitions = new RowDefinitions("*,*") };
                panels.Children.Add(stackPanel); panels.Children.Add(localsPanel); Grid.SetRow(localsPanel, 1);
                panelsWindow = new Window { Width = 850, Height = 500, Title = "Call Stack and Locals integration test", Content = panels };
                panelsWindow.Show();
                var shell = MainWindowViewModel.Instance;
                var source = Path.GetFullPath("tests/DebuggerIntegration/Target/Program.cs");
                shell.OpenSolution(Path.GetFullPath("tests/DebuggerIntegration/Target/DeepTarget.csproj"));
                await Until(() => shell.SelectedStartupProject != null, "project loaded");
                shell.Open(source);
                await Until(() => main.GetVisualDescendants().OfType<XamlEditorView>().Any(v => v.Editor?.Text.Contains("AddTwo") == true), "editor loaded");
                var view = main.GetVisualDescendants().OfType<XamlEditorView>().First(v => v.Editor?.Text.Contains("AddTwo") == true);
                var toolbar = main.GetVisualDescendants().OfType<ToolbarView>().First();
                foreach (var line in BreakpointService.Instance.GetBreakpoints(source).ToArray()) BreakpointService.Instance.ToggleBreakpoint(source, line);
                view.Editor!.TextArea.Caret.Line = 4;
                Key(view.Editor.TextArea, Avalonia.Input.Key.F9);
                Key(main, Avalonia.Input.Key.F5);
                await Until(() => vm.IsPaused && vm.CurrentLine == 4, "initial breakpoint");
                Check(true, "real breakpoint before method call");
                await Until(() => locals.Variables.Any(v => v.Name == "value" && v.Value == "40"), "locals at initial breakpoint");
                Check(callStack.Frames.Count > 0, "Call Stack populated at breakpoint");
                Check(true, "Locals displays value=40 at breakpoint");
                await Until(() => localsPanel.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "40"), "Locals rendered value=40");
                Check(localsPanel.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "value"), "Locals renders variable name with actual value");
                Capture(panelsWindow, "panels-at-breakpoint");

                int previous = stopCount;
                Key(main, Avalonia.Input.Key.F11);
                await Until(() => stopCount > previous && vm.CurrentLine >= 12, "step into method");
                var stack = await dbg.StackTraceAsync(thread);
                Check(stack.GetProperty("stackFrames")[0].GetProperty("name").GetString()!.Contains("AddTwo"), "F11 enters AddTwo frame");
                var frame = stack.GetProperty("stackFrames")[0].GetProperty("id").GetInt32();
                Check(await dbg.EvaluateAsync("input", frame) == "40", "evaluate method argument input=40");
                await Until(() => locals.Variables.Any(v => v.Name == "input" && v.Value == "40"), "locals updated after Step Into");
                Check(callStack.Frames[0].Name.Contains("AddTwo"), "Call Stack updates to AddTwo after Step Into");
                Check(!locals.Variables.Any(v => v.Name == "value"), "Locals replaces caller variables with method variables");
                await Until(() => stackPanel.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("AddTwo") == true), "Call Stack renders new frame");
                Check(localsPanel.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "input"), "Locals renders current method argument");
                Capture(panelsWindow, "panels-after-step");
                while (vm.CurrentLine < 14)
                {
                    previous = stopCount;
                    Key(main, Avalonia.Input.Key.F10);
                    await Until(() => stopCount > previous && vm.IsPaused, "step through method assignment");
                }
                await Until(() => locals.Variables.Any(v => v.Name == "result" && v.Value == "42"), "Locals updates result after assignment");
                await Until(() => localsPanel.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "42"), "Locals renders updated result=42");
                Check(true, "Locals renders result=42 after Step Over");
                Capture(panelsWindow, "panels-result-42");
                shell.Factory.SetActiveDockable(callStack);
                await Until(() => main.GetVisualDescendants().OfType<Scadix.Designer.Views.Tools.CallStackView>().Any(), "Call Stack dock tab opens");
                Capture(main, "call-stack-docked");
                Check(true, "Call Stack opens in Designer dock");
                shell.Factory.SetActiveDockable(locals);
                await Until(() => main.GetVisualDescendants().OfType<Scadix.Designer.Views.Tools.LocalsView>().Any(v => v.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "42")), "Locals dock tab renders updated value");
                Capture(main, "locals-docked");
                Check(true, "Locals opens in Designer dock with result=42");
                previous = stopCount;
                Key(main, Avalonia.Input.Key.F11, KeyModifiers.Shift);
                await Until(() => stopCount > previous && vm.CurrentLine < 11, "step out");
                stack = await dbg.StackTraceAsync(thread);
                Check(!stack.GetProperty("stackFrames")[0].GetProperty("name").GetString()!.Contains("AddTwo"), "Shift+F11 returns to caller");

                int logStart = logs.Count;
                BreakpointService.Instance.ToggleBreakpoint(source, 8);
                await Until(() => logs.Skip(logStart).Any(x => x.Contains("Live sync:")), "live breakpoint add sync");
                Key(main, Avalonia.Input.Key.F5);
                await Until(() => vm.CurrentLine == 8, "new live breakpoint hit");
                Check(true, "breakpoint added during session is hit at line 8");
                logStart = logs.Count;
                BreakpointService.Instance.ToggleBreakpoint(source, 8);
                await Until(() => logs.Skip(logStart).Any(x => x.Contains("Live sync:")), "live remove sync");
                previous = stopCount;
                Key(main, Avalonia.Input.Key.F5);
                await Until(() => logs.Any(x => x.Contains("TICK=1")), "target running after continue");
                Check(dbg.IsDebugging, "target is still running during resume checks");
                Check(!vm.IsPaused, "F5 clears Paused state while target runs");
                Check(vm.CurrentLine == 0 && string.IsNullOrEmpty(vm.CurrentFile), "F5 clears stale source location while target runs");
                Check(toolbar.FindControl<Button>("BtnStepOver")?.IsEnabled == false, "Step Over disabled while target runs");
                Check(toolbar.FindControl<Button>("BtnPause")?.IsEnabled == true, "Pause enabled while target runs");
                Check(callStack.Frames.Count == 0 && locals.Variables.Count == 0, "Call Stack and Locals clear while running");
                await Until(() => !localsPanel.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "input" || t.Text == "value"), "Locals clears rendered variables");
                Capture(panelsWindow, "panels-running"); Capture(main, "running-state");
                await Until(() => !dbg.IsDebugging && !vm.IsDebugging, "target exits");
                Check(stopCount == previous, "removed live breakpoint never fires on remaining loop iterations");

                for (int cycle = 1; cycle <= 3; cycle++)
                {
                    Key(main, Avalonia.Input.Key.F5);
                    await Until(() => vm.IsPaused && vm.CurrentLine == 4, "restart breakpoint " + cycle);
                    var currentStack = await dbg.StackTraceAsync(thread); var currentFrame = currentStack.GetProperty("stackFrames")[0].GetProperty("id").GetInt32(); targetIds.Add(int.Parse(await dbg.EvaluateAsync("System.Environment.ProcessId", currentFrame))); Key(main, Avalonia.Input.Key.F5, KeyModifiers.Shift);
                    await Until(() => !vm.IsDebugging && vm.CurrentLine == 0, "stop " + cycle);
                    Check(true, "start/breakpoint/stop cycle " + cycle);
                    await Task.Delay(200);
                }
                Key(main, Avalonia.Input.Key.F5);
                await Until(() => vm.IsPaused && vm.CurrentLine == 4, "pause test breakpoint");
                logStart = logs.Count;
                Key(main, Avalonia.Input.Key.F5);
                await Until(() => logs.Skip(logStart).Any(x => x.Contains("TICK=0")), "pause test running");
                previous = stopCount;
                toolbar.FindControl<Button>("BtnPause")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                try
                {
                    await Until(() => stopCount > previous && vm.IsPaused, "Pause stopped event", 4);
                    Check(true, "Continue -> Pause stops real target");
                    Check(toolbar.FindControl<Button>("BtnStepOver")?.IsEnabled == true, "Pause restores Step button");
                    Capture(main, "paused");
                    Key(main, Avalonia.Input.Key.F5);
                }
                catch (Exception ex) { Check(false, ex.Message); }
                await Until(() => !dbg.IsDebugging && !vm.IsDebugging, "pause test exits");
                Check(true, "Continue after Pause completes target");
                await Task.Delay(1500);
                foreach (int targetId in targetIds) { bool exited; try { using var process = System.Diagnostics.Process.GetProcessById(targetId); exited = process.HasExited; } catch (ArgumentException) { exited = true; } Check(exited, "actual target PID " + targetId + " exited after stop"); }
            }
            catch (Exception ex) { Check(false, ex.ToString()); }
            finally { dbg.StopDebugging(); panelsWindow?.Close(); main?.Close(); stop.Cancel(); }
        });
        Dispatcher.UIThread.MainLoop(stop.Token);
        Console.WriteLine("TOTAL FAILURES: " + failures);
        return failures == 0 ? 0 : 1;
    }

    // Deliver actual DAP messages to the service parser in a deterministic order.
    // A reply to an older command must not override a newer stopped event.
    static void CheckProtocolOrdering(DebuggerService debugger)
    {
        var dispatch = typeof(DebuggerService).GetMethod("Dispatch",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        bool paused = false;
        EventHandler resumed = (_, _) => paused = false;
        EventHandler<StoppedEventArgs> stopped = (_, _) => paused = true;
        debugger.Continued += resumed;
        debugger.Stopped += stopped;
        try
        {
            foreach (var json in new[]
            {
                "{\"type\":\"event\",\"event\":\"continued\",\"body\":{\"threadId\":1}}",
                "{\"type\":\"event\",\"event\":\"stopped\",\"body\":{\"threadId\":1,\"reason\":\"step\"}}",
                "{\"type\":\"response\",\"command\":\"next\",\"request_seq\":-1,\"success\":true}"
            })
                dispatch.Invoke(debugger, new object[] { System.Text.Json.Nodes.JsonNode.Parse(json)! });
            Check(paused, "late step response does not override a newer stopped event");
        }
        finally
        {
            debugger.Continued -= resumed;
            debugger.Stopped -= stopped;
        }
    }
}
