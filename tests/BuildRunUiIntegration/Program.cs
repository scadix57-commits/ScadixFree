using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Scadix.Designer;
using Scadix.Designer.Services;

internal static class Program
{
    [STAThread]
    static int Main()
    {
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        using var stop = new CancellationTokenSource();
        var failures = 0;
        void Check(bool ok, string message)
        {
            Console.WriteLine((ok ? "PASS: " : "FAIL: ") + message);
            if (!ok) failures++;
        }
        Dispatcher.UIThread.Post(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "Scadix UI " + Guid.NewGuid());
            Directory.CreateDirectory(root);
            Window? window = null;
            var vm = MainWindowViewModel.Instance;
            try
            {
                var target = Path.Combine(root, "Chosen.proj");
                File.WriteAllText(target, "<Project><Target Name=\"Build\"><ReadLinesFromFile File=\"saved.txt\"><Output TaskParameter=\"Lines\" ItemName=\"Saved\" /></ReadLinesFromFile><Message Text=\"@(Saved)\" Importance=\"high\" /></Target></Project>");
                vm.SolutionTree.Add(new SolutionNode { FilePath = target });
                var file = Path.Combine(root, "saved.txt");
                File.WriteAllText(file, "OLD_CONTENT");
                var doc = new Document(file) { Text = "SAVED_BEFORE_BUILD" };
                vm.Documents.Add(doc);
                var toolbar = new ToolbarView();
                window = new Window { DataContext = vm, Content = toolbar, Width = 1500, Height = 100 };
                window.Show();
                var build = toolbar.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Build"));
                var cancel = toolbar.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel"));
                var task = vm.RunDotnetCommand("build");
                Check(vm.IsBuildRunning && !vm.BuildCommand.CanExecute(null) && vm.CancelBuildCommand.CanExecute(null), "Commands reflect active operation");
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                Console.WriteLine($"UI state: busy={vm.IsBuildRunning}, build={build.IsEnabled}/{build.IsEffectivelyEnabled}, cancel={cancel.IsVisible}/{cancel.IsEnabled}, context={toolbar.DataContext?.GetType().Name}");
                Check(!build.IsEffectivelyEnabled && cancel.IsVisible && cancel.IsEffectivelyEnabled, "Toolbar disables Build and exposes Cancel");
                await task;
                Check(!doc.IsDirty && File.ReadAllText(file) == "SAVED_BEFORE_BUILD" && BuildOutputService.Instance.BuildLogs.Any(x => x.Contains("SAVED_BEFORE_BUILD")), "Dirty document saved before process reads it");
                Check(vm.BuildStatus == "Build succeeded." && !vm.IsBuildRunning && vm.BuildCommand.CanExecute(null), "Success restores commands and status");
                doc.Text = "UNSAVED_CONTENT";
                using (var locked = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    await vm.RunDotnetCommand("build");
                Check(vm.BuildStatus.Contains("failed") && !vm.IsBuildRunning && !BuildOutputService.Instance.BuildLogs.Any(x => x.StartsWith("Target:")), "Save failure aborts process and restores state");
                vm.Documents.Remove(doc);
                vm.SelectedStartupProject = null;
                await vm.RunDotnetCommand("run");
                Check(vm.BuildStatus == "Select a startup project to run.", "Run without startup project reports actionable message");
                Console.WriteLine($"TOTAL FAILURES: {failures}");
            }
            catch (Exception ex) { Console.WriteLine(ex); failures++; }
            finally
            {
                window?.Close();
                Directory.Delete(root, true);
                stop.Cancel();
            }
        });
        Dispatcher.UIThread.MainLoop(stop.Token);
        return failures == 0 ? 0 : 1;
    }
}
