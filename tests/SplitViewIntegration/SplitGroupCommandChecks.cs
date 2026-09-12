using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.Designer;

internal static class SplitGroupCommandChecks
{
    public static async Task Run(Document doc, DocumentView view)
    {
        doc.Mode = DocumentMode.Split;
        var failures = 0;
        void Check(bool ok, string message)
        {
            Console.WriteLine((ok ? "PASS: " : "FAIL: ") + message);
            if (!ok) failures++;
        }
        bool Near(double a, double b) => Math.Abs(a - b) < .1;
        var editor = view.GetVisualDescendants().OfType<XamlEditorView>().Single().Editor!;
        const string source = "<UserControl xmlns='https://github.com/avaloniaui' UseLayoutRounding='False' Width='400' Height='300'>\r\n<!-- keep -->\r\n<Canvas><Button Content='A' Width='40' Height='20' Canvas.Left='20' Canvas.Top='24' /><Button Content='B' Width='60' Height='40' Canvas.Left='100' Canvas.Top='80' /><Button Content='C' Width='20' Height='30' Canvas.Left='260' Canvas.Top='210' /></Canvas></UserControl>";
        Button Control(string name) => doc.DesignSurface.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, name));
        ISplitGroupCommandService Service() => doc.DesignContext!.Services.GetService<ISplitGroupCommandService>()!;
        void Select(params string[] names)
        {
            var components = doc.DesignContext!.Services.Component;
            doc.SelectionService!.SetSelectedComponents(names.Select(n => components.GetDesignItem(Control(n))).ToArray(), SelectionTypes.Replace);
        }
        async Task Load(string text = source)
        {
            editor.Text = text;
            await Task.Delay(750);
            Select("B");
            Select("A", "B", "C");
            editor.Document.UndoStack.ClearAll();
        }
        var bar = view.FindControl<StackPanel>("SplitGroupCommandBar");
        foreach (var alignment in Enum.GetValues<GroupAlignment>())
        {
            await Load();
            var service = Service();
            Check(service is { CanAlign: true, CanDistribute: true }, "Three siblings enable commands");
            var primary = Control("B").Bounds;
            Check(service!.Align(alignment), alignment + " commits");
            await Task.Delay(100);
            double Edge(Rect r) => alignment switch
            {
                GroupAlignment.Left => r.Left, GroupAlignment.HorizontalCenter => r.Center.X,
                GroupAlignment.Right => r.Right, GroupAlignment.Top => r.Top,
                GroupAlignment.VerticalCenter => r.Center.Y, _ => r.Bottom
            };
            Check(new[] { "A", "B", "C" }.All(n => Near(Edge(Control(n).Bounds), Edge(primary)))
                && Control("B").Bounds == primary, alignment + " uses primary B as reference");
            Check(doc.SelectionService!.SelectionCount == 3 && Equals(((Button)doc.SelectionService.PrimarySelection.Component).Content, "B"),
                alignment + " preserves selection and primary after refresh");
            Check(!ReferenceEquals(Service(), service) && !service.CanAlign && !service.Align(GroupAlignment.Left),
                alignment + " retires the previous preview service");
            var changed = doc.Text;
            Check(changed.Contains("<!-- keep -->") && changed.Contains("Width='60' Height='40'"), alignment + " preserves unrelated source");
            doc.UndoCommand.Execute(null);
            Check(doc.Text == source && !editor.Document.UndoStack.CanUndo, alignment + " is one Undo entry");
            await Task.Delay(750);
            doc.RedoCommand.Execute(null);
            Check(doc.Text == changed, alignment + " supports one-step Redo");
            await Task.Delay(750);
        }
        foreach (var direction in Enum.GetValues<GroupDistribution>())
        {
            await Load();
            var first = Control("A").Bounds;
            var last = Control("C").Bounds;
            Check(Service().Distribute(direction), direction + " distribution commits");
            await Task.Delay(100);
            var middle = Control("B").Bounds;
            Check(Control("A").Bounds == first && Control("C").Bounds == last, direction + " preserves outer controls");
            Check(direction == GroupDistribution.Horizontal
                ? Near(middle.Left - first.Right, last.Left - middle.Right)
                : Near(middle.Top - first.Bottom, last.Top - middle.Bottom), direction + " equalizes gaps");
            var changed = doc.Text;
            doc.UndoCommand.Execute(null);
            Check(doc.Text == source && !editor.Document.UndoStack.CanUndo, direction + " distribution is one Undo entry");
            await Task.Delay(750);
            doc.RedoCommand.Execute(null);
            Check(doc.Text == changed, direction + " distribution supports Redo");
            await Task.Delay(750);
        }
        await Load();
        Check(bar is { IsVisible: true } && bar.Children.OfType<Button>().Count() == 8, "Eligible Split group shows all eight commands");
        Select("A", "B");
        Check(Service().CanAlign && !Service().CanDistribute && bar!.Children.OfType<Button>().Count(b => b.IsVisible && b.IsEnabled) == 6,
            "Two siblings expose only alignment");
        var unchanged = doc.Text;
        Check(!Service().Distribute(GroupDistribution.Horizontal) && doc.Text == unchanged && !editor.Document.UndoStack.CanUndo,
            "Two-item distribution does not mutate source");
        foreach (var names in new[] { new[] { "A" }, Array.Empty<string>() })
        {
            Select(names);
            Check(!Service().CanAlign && !Service().CanDistribute && !bar!.IsVisible && !Service().Align(GroupAlignment.Left),
                names.Length + " selected items disable and hide commands");
        }
        await Load(source.Replace("Canvas.Top='210'", "Canvas.Top='{Binding ProtectedTop}'"));
        unchanged = doc.Text;
        Check(!Service().CanAlign && !Service().CanDistribute && !bar!.IsVisible
            && !Service().Align(GroupAlignment.Right) && !Service().Distribute(GroupDistribution.Vertical)
            && doc.Text == unchanged && !editor.Document.UndoStack.CanUndo, "Protected position rejects commands atomically");
        await Load(source.Replace("<Button Content='C'", "<Canvas><Button Content='C'").Replace(" /></Canvas></UserControl>", " /></Canvas></Canvas></UserControl>"));
        Check(!Service().CanAlign && !Service().CanDistribute && !bar!.IsVisible, "Mixed parents disable commands");

        await Load(source.Replace("Width='20'", "Width='{Binding ProtectedWidth}'"));
        Check(Service().CanAlign && Service().Align(GroupAlignment.Left), "Protected size still permits position-only alignment");
        Check(doc.Text.Contains("Width='{Binding ProtectedWidth}'"), "Alignment retains protected size expression");
        await Load();
        Check(Service().Align(GroupAlignment.Left), "Initial alignment changes geometry");
        editor.Document.UndoStack.ClearAll();
        var alignedService = Service();
        unchanged = doc.Text;
        Check(!alignedService.Align(GroupAlignment.Left) && doc.Text == unchanged
            && !editor.Document.UndoStack.CanUndo && ReferenceEquals(Service(), alignedService),
            "Already aligned group creates no source edit or preview reload");

        const string gridSource = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'><Grid><Button Content='A' Width='40' Height='20' HorizontalAlignment='Left' VerticalAlignment='Top' Margin='20,24,0,0'/><Button Content='B' Width='60' Height='40' HorizontalAlignment='Left' VerticalAlignment='Top' Margin='100,80,0,0'/><Button Content='C' Width='20' Height='30' HorizontalAlignment='Left' VerticalAlignment='Top' Margin='260,210,0,0'/></Grid></UserControl>";
        await Load(gridSource);
        Check(Service().CanAlign && Service().CanDistribute && Service().Align(GroupAlignment.Right), "Grid siblings enable and commit commands");
        Check(Near(Control("A").Bounds.Right, 160) && Near(Control("C").Bounds.Right, 160)
            && !doc.Text.Contains("Canvas.Left"), "Grid alignment edits margins relative to primary");
        await Load(gridSource.Replace("Margin='260,210,0,0'", "Margin='{Binding ProtectedMargin}'"));
        Check(!Service().CanAlign && !Service().CanDistribute, "Protected Grid margin rejects commands");

        await Load();
        var staleService = Service();
        editor.Text = "<UserControl";
        Check(!staleService.CanAlign && !staleService.Align(GroupAlignment.Right) && !bar!.IsVisible,
            "Source change immediately disables stale commands");
        await Task.Delay(750);
        Check(doc.SelectionService == null && !bar!.IsVisible, "Failed preview leaves commands hidden without a selection service");

        await Load();
        var oldService = Service();
        var host = (Window)TopLevel.GetTopLevel(view)!;
        host.Content = null;
        Check(!oldService.CanAlign && !oldService.Align(GroupAlignment.Right), "Detached view retires command service");
        host.Content = view;
        await Task.Delay(750);
        Select("B");
        Select("A", "B", "C");
        Check(!ReferenceEquals(oldService, Service()) && Service().CanAlign && bar!.IsVisible, "Reattached view recreates working commands");
        foreach (var mode in new[] { DocumentMode.Xaml, DocumentMode.Design })
        {
            oldService = Service();
            doc.Mode = mode;
            await Task.Delay(100);
            Check(!bar!.IsVisible && !oldService.CanAlign && !oldService.Align(GroupAlignment.Left), mode + " hides and disables Split commands");
            doc.Mode = DocumentMode.Split;
            await Task.Delay(750);
            Select("B");
            Select("A", "B", "C");
        }
        view.FindControl<Button>("AlignRightButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(100);
        Check(Near(Control("A").Bounds.Right, Control("B").Bounds.Right)
            && Near(Control("C").Bounds.Right, Control("B").Bounds.Right), "Command bar executes alignment");
        Console.WriteLine($"TOTAL GROUP COMMAND FAILURES: {failures}");
        if (failures != 0) throw new Exception($"{failures} group command failures");
    }
}
