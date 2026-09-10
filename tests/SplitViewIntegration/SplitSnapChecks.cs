using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.Designer;

internal static class SplitSnapChecks
{
    public static async Task Run(Document doc, DocumentView view)
    {
        int failures = 0;
        void Check(bool ok, string message)
        {
            Console.WriteLine((ok ? "PASS: " : "FAIL: ") + message);
            if (!ok) failures++;
        }
        bool Near(double a, double b) => Math.Abs(a - b) < .1;
        var editor = view.GetVisualDescendants().OfType<XamlEditorView>().Single().Editor!;
        var overlay = view.FindControl<Canvas>("SplitResizeOverlay")!;
        const string source = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'>\r\n<!-- keep -->\r\n<Canvas><Button Content='Test' Width='120' Height='60' Canvas.Left='40' Canvas.Top='50' /></Canvas></UserControl>";
        async Task Load(string text)
        {
            editor.Text = text;
            await Task.Delay(750);
            editor.Select(text.IndexOf("<Button", StringComparison.Ordinal) + 2, 0);
            await Task.Delay(100);
            editor.Document.UndoStack.ClearAll();
        }
        Button Selected() => (Button)doc.SelectionService!.PrimarySelection!.Component;
        Control Handle(string name) => overlay.Children.OfType<Control>().Single(c => c.Name == name);
        void Key(Key key, KeyModifiers modifiers = KeyModifiers.None)
        {
            var focused = TopLevel.GetTopLevel(view)!.FocusManager!.GetFocusedElement() as InputElement;
            focused?.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });
        }
        void Drag(string name, Vector delta, KeyModifiers modifiers = KeyModifiers.None, bool cancel = false, Action? during = null)
        {
            var handle = Handle(name);
            var root = (Visual)TopLevel.GetTopLevel(handle)!;
            var start = handle.TranslatePoint(new Point(4, 4), root)!.Value;
            var button = Selected();
            var screenDelta = button.TranslatePoint(new Point(delta.X, delta.Y), root)!.Value - button.TranslatePoint(default, root)!.Value;
            using var pointer = new Pointer(29, PointerType.Mouse, true);
            handle.RaiseEvent(new PointerPressedEventArgs(handle, pointer, root, start, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), modifiers, 1));
            handle.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, handle, pointer, root, start + screenDelta, 1,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), modifiers));
            during?.Invoke();
            if (cancel) Key(Avalonia.Input.Key.Escape);
            handle.RaiseEvent(new PointerReleasedEventArgs(handle, pointer, root, start + screenDelta, 2,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), modifiers, MouseButton.Left));
        }
        async Task UndoRedo(string original, string changed, string label)
        {
            await Task.Delay(750);
            doc.UndoCommand.Execute(null);
            Check(doc.Text == original && !editor.Document.UndoStack.CanUndo, label + " one Undo restores complete operation");
            await Task.Delay(750);
            doc.RedoCommand.Execute(null);
            Check(doc.Text == changed, label + " Redo restores operation");
            await Task.Delay(750);
        }
        var service = doc.DesignContext!.Services.GetService<Scadix.AxamlDesign.ISplitResizeOverlayService>()!;
        var readout = view.FindControl<Border>("SnapReadout")!;
        var readoutText = view.FindControl<TextBlock>("SnapReadoutText")!;
        Check(service.SnapEnabled && Near(service.SnapGridSize, 8), "Service defaults: enabled, grid 8");
        foreach (var name in new[] { "SplitMoveHandle", "SplitBorderDrag" })
        foreach (var alt in new[] { false, true })
        {
            await Load(source);
            Drag(name, new Vector(13, 13), alt ? KeyModifiers.Alt : KeyModifiers.None, during: () => {
                Check(doc.Text == source, name + " defers source edit");
                Check(readout.IsVisible, name + " readout visible during drag");
                if (!alt) Check(readoutText.Text == "X: 56  Y: 64", "Move readout shows snapped control coordinates (actual " + readoutText.Text + ")");
                if (!alt && name == "SplitMoveHandle")
                {
                    view.UpdateLayout();
                    Check(Near(readout.Bounds.X, Canvas.GetLeft(readout)) && Near(readout.Bounds.Y, Canvas.GetTop(readout)),
                        "Readout is laid out at requested drag location (actual " + readout.Bounds.Position + ", requested " + Canvas.GetLeft(readout) + "," + Canvas.GetTop(readout) + ")");
                }
            });
            Check(!readout.IsVisible, name + " readout hidden on release");
            var changed = doc.Text;
            await Task.Delay(750);
            var p = Selected().Bounds.Position;
            Check(Near(p.X, alt ? 53 : 56) && Near(p.Y, alt ? 63 : 64), name + " Alt=" + alt + " final position (actual " + p + ")");
            await UndoRedo(source, changed, name + " Alt=" + alt);
        }
        foreach (var name in new[] { "Left", "Top", "Right", "Bottom", "TopLeft", "TopRight", "BottomLeft", "BottomRight" })
        foreach (var alt in new[] { false, true })
        {
            await Load(source);
            var before = Selected().Bounds;
            bool horizontal = name != "Top" && name != "Bottom";
            bool vertical = name != "Left" && name != "Right";
            var w = horizontal ? 120 + (name.Contains("Left") ? -13 : 13) : 120;
            var h = vertical ? 60 + (name.Contains("Top") ? -13 : 13) : 60;
            double expectedW = !alt && horizontal ? Math.Round(w / 8d) * 8 : w;
            double expectedH = !alt && vertical ? Math.Round(h / 8d) * 8 : h;
            Drag("SplitResize" + name, new Vector(13,13), alt ? KeyModifiers.Alt : KeyModifiers.None, during: () => {
                Check(readout.IsVisible, name + " resize readout visible");
                Check(readoutText.Text == $"W: {expectedW:0}  H: {expectedH:0}", name + " resize readout matches intended dimensions (actual " + readoutText.Text + ")");
            });
            Check(!readout.IsVisible, name + " resize readout hidden on release");
            var changed = doc.Text;
            await Task.Delay(750);
            var after = Selected().Bounds;
            Check(Near(after.Width,expectedW) && Near(after.Height,expectedH), name + " Alt=" + alt + " resize dimensions (actual " + after.Size + ")");
            Check(Near(name.Contains("Left") ? after.Right : after.X, name.Contains("Left") ? before.Right : before.X) &&
                Near(name.Contains("Top") ? after.Bottom : after.Y, name.Contains("Top") ? before.Bottom : before.Y), name + " opposite edge stays fixed");
            if (name == "BottomRight") await UndoRedo(source, changed, "Resize Alt=" + alt);
        }
        foreach (var name in new[] { "SplitMoveHandle", "SplitBorderDrag", "SplitResizeTopLeft" })
        {
            await Load(source);
            Drag(name, new Vector(13,13), cancel: true);
            Check(doc.Text == source && !editor.Document.UndoStack.CanUndo, name + " Escape cancels without undo entry");
            Check(!readout.IsVisible, name + " Escape hides readout");
        }
        foreach (var modifiers in new[] { KeyModifiers.None, KeyModifiers.Shift })
        foreach (var key in new[] { Avalonia.Input.Key.Left, Avalonia.Input.Key.Right, Avalonia.Input.Key.Up, Avalonia.Input.Key.Down })
        {
            await Load(source);
            overlay.Focus();
            Key(key, modifiers);
            var step = modifiers == KeyModifiers.Shift ? 10 : 1;
            var p = Selected().Bounds.Position;
            Check(Near(p.X, 40 + (key == Avalonia.Input.Key.Left ? -step : key == Avalonia.Input.Key.Right ? step : 0)) &&
                Near(p.Y, 50 + (key == Avalonia.Input.Key.Up ? -step : key == Avalonia.Input.Key.Down ? step : 0)), modifiers + " " + key + " moves without snap (actual " + p + ")");
            doc.UndoCommand.Execute(null);
            Check(doc.Text == source && !editor.Document.UndoStack.CanUndo, "Arrow single-step Undo");
        }
        foreach (var enabled in new[] { false, true })
        {
            await Load(source.Replace("Canvas.Top='50'", "Canvas.Top='40'"));
            service.SnapEnabled = enabled;
            service.SnapGridSize = 16;
            Drag("SplitMoveHandle", new Vector(13,13));
            await Task.Delay(750);
            var p = Selected().Bounds.Position;
            Check(Near(p.X,enabled ? 48 : 53) && Near(p.Y,enabled ? 48 : 53), "Runtime service move enabled=" + enabled + " grid16 (actual " + p + ")");
            await Load(source);
            Drag("SplitResizeBottomRight", new Vector(13,13));
            await Task.Delay(750);
            Check(Near(Selected().Bounds.Width,enabled ? 128 : 133) && Near(Selected().Bounds.Height,enabled ? 80 : 73), "Runtime service resize enabled=" + enabled + " grid16");
        }
        service.SnapEnabled = true;
        service.SnapGridSize = 8;
        await Load(source);
        Drag("SplitResizeBottomRight", new Vector(13,0), KeyModifiers.Control);
        await Task.Delay(750);
        Check(Near(Selected().Bounds.Width / Selected().Bounds.Height, 2), "Ctrl resize preserves aspect ratio with snap (actual " + Selected().Bounds.Size + ")");
        Console.WriteLine("TOTAL SNAP FAILURES: " + failures);
        if (failures > 0) throw new Exception("Snap checks failed: " + failures);
    }
}
