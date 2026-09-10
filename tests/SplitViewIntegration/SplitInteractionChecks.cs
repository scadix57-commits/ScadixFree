using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.Designer;

internal static class SplitInteractionChecks
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
        doc.DesignContext!.Services.GetService<Scadix.AxamlDesign.ISplitResizeOverlayService>()!.SnapEnabled = false;
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
        if (!Environment.GetCommandLineArgs().Contains("--extra-only"))
        {
            foreach (var (name, delta, modifiers) in new[] {
                ("Left", new Vector(-20, 0), KeyModifiers.None),
                ("Top", new Vector(0, -10), KeyModifiers.None),
                ("TopLeft", new Vector(-20, -10), KeyModifiers.None),
                ("TopLeft", new Vector(-20, -3), KeyModifiers.Control) })
            {
                await Load(source);
                var before = Selected().Bounds;
                var label = "Canvas " + name + " " + modifiers;
                var expectedWidth = name == "Top" ? 120 : 140;
                var expectedHeight = name == "Left" ? 60 : 70;
                Drag("SplitResize" + name, delta, modifiers, during: () => {
                    Check(doc.Text == source, label + " defers source until release");
                    var border = Handle("SplitBorderDrag");
                    var expected = Selected().TranslatePoint(new Point(120, 60), overlay)!.Value;
                    Check(Near(Canvas.GetLeft(border) + border.Width, expected.X) && Near(Canvas.GetTop(border) + border.Height, expected.Y), label + " ghost keeps opposite edges fixed");
                });
                var changed = doc.Text;
                await Task.Delay(750);
                var after = Selected().Bounds;
                Check(Near(after.Width, expectedWidth) && Near(after.Height, expectedHeight), label + " changes dimensions");
                Check(Near(after.Right, before.Right) && Near(after.Bottom, before.Bottom), label + " final opposite edges stay fixed");
                await UndoRedo(source, changed, label);
                await Load(source);
                Drag("SplitResize" + name, delta, modifiers, cancel: true);
                Check(doc.Text == source && !editor.Document.UndoStack.CanUndo, label + " Escape cancels without undo entry");
            }
            var grid = source.Replace("Canvas>", "Grid>").Replace("Canvas.Left='40' Canvas.Top='50'", "Margin='10,20,30,40'");
            foreach (var (alignment, expectedMargin) in new[] {
                ("HorizontalAlignment='Right' VerticalAlignment='Top'", new Thickness(10,30,10,40)),
                ("HorizontalAlignment='Left' VerticalAlignment='Bottom'", new Thickness(30,20,30,30)),
                ("HorizontalAlignment='Right' VerticalAlignment='Bottom'", new Thickness(10,20,10,30)),
                ("HorizontalAlignment='Center' VerticalAlignment='Center'", new Thickness(50,40,30,40)) })
            {
                var fixture = grid.Replace("<Button", "<Button " + alignment);
                await Load(fixture);
                var before = Selected().Bounds.Position;
                Check(overlay.Children.OfType<Control>().Any(c => c.Name == "SplitMoveHandle"), alignment + " has move handle");
                Drag("SplitMoveHandle", new Vector(20,10));
                var changed = doc.Text;
                await Task.Delay(750);
                Check(Selected().Margin == expectedMargin, alignment + " correct margins (actual " + Selected().Margin + ")");
                var delta = Selected().Bounds.Position - before;
                Check(Near(delta.X,20) && Near(delta.Y,10), alignment + " actual movement 20,10 (actual " + delta + ")");
                await UndoRedo(fixture, changed, alignment);
                await Load(fixture);
                Drag("SplitMoveHandle", new Vector(20,10), cancel: true);
                Check(doc.Text == fixture && !editor.Document.UndoStack.CanUndo, alignment + " Escape cancels move");
            }
            foreach (var modifiers in new[] { KeyModifiers.None, KeyModifiers.Shift })
            foreach (var key in new[] { Avalonia.Input.Key.Left, Avalonia.Input.Key.Right, Avalonia.Input.Key.Up, Avalonia.Input.Key.Down })
            {
                await Load(source);
                Drag("SplitMoveHandle", default); // click/release gives the real target keyboard focus
                var before = Selected().Bounds.Position;
                Key(key, modifiers);
                var changed = doc.Text;
                await Task.Delay(750);
                var delta = Selected().Bounds.Position - before;
                var step = modifiers == KeyModifiers.Shift ? 10 : 1;
                var expected = key switch {
                    Avalonia.Input.Key.Left => new Vector(-step,0),
                    Avalonia.Input.Key.Right => new Vector(step,0),
                    Avalonia.Input.Key.Up => new Vector(0,-step),
                    _ => new Vector(0,step)
                };
                Check(Near(delta.X,expected.X) && Near(delta.Y,expected.Y), modifiers + " " + key + " moves selection " + expected + " (actual " + delta + ")");
                if (changed != source) await UndoRedo(source, changed, modifiers + " " + key);
            }
            await Load(source);
            Drag("SplitMoveHandle", new Vector(20,10), cancel: true, during: () => Key(Avalonia.Input.Key.Right));
            Check(doc.Text == source && !editor.Document.UndoStack.CanUndo, "Arrow during drag then Escape leaves source unchanged");
        }
        await Load(source);
        var selectionOverlay = view.FindControl<Border>("PreviewSelectionOverlay")!;
        var selectionPoint = Selected().TranslatePoint(new Point(10,10), selectionOverlay)!.Value;
        using (var pointer = new Pointer(31, PointerType.Mouse, true))
            selectionOverlay.RaiseEvent(new PointerPressedEventArgs(selectionOverlay, pointer, selectionOverlay, selectionPoint, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
        Key(Avalonia.Input.Key.Right);
        await Task.Delay(750);
        Check(Near(Selected().Bounds.X,41), "Arrow works immediately after preview selection");
        Key(Avalonia.Input.Key.Right);
        await Task.Delay(750);
        Check(Near(Selected().Bounds.X,42), "Keyboard focus survives preview reload for the next Arrow");
        doc.UndoCommand.Execute(null);
        await Task.Delay(750);
        Check(Near(Selected().Bounds.X,41), "Repeated Arrow operations undo separately");
        await Load(source);
        Drag("SplitMoveHandle", default);
        Key(Avalonia.Input.Key.Right);
        Key(Avalonia.Input.Key.Right);
        await Task.Delay(750);
        Check(Near(Selected().Bounds.X,42), "Consecutive Arrow presses before debounce each move one pixel");
        doc.UndoCommand.Execute(null);
        await Task.Delay(750);
        Check(Near(Selected().Bounds.X,41), "Consecutive Arrow presses remain separate Undo operations");
        foreach (var handleName in new[] { "SplitMoveHandle", "SplitBorderDrag", "SplitResizeRight" })
        {
            await Load(source);
            Drag(handleName, new Vector(10,0));
            await Task.Delay(750);
            var x = Selected().Bounds.X;
            Key(Avalonia.Input.Key.Right);
            await Task.Delay(750);
            Check(Near(Selected().Bounds.X,x+1), "Arrow remains available after " + handleName + " commit/reload");
        }
        foreach (var property in new[] { "Canvas.Left", "Canvas.Top" })
        {
            var fixture = source.Replace(property == "Canvas.Left" ? "Canvas.Left='40'" : "Canvas.Top='50'", property + "='{Binding Position}'");
            await Load(fixture);
            Drag(property == "Canvas.Left" ? "SplitResizeLeft" : "SplitResizeTop", new Vector(10,10));
            Check(doc.Text == fixture && !editor.Document.UndoStack.CanUndo, "Resize rejects required protected position " + property);
        }
        foreach (var alignment in new[] { "HorizontalAlignment='Left' VerticalAlignment='Top'", "HorizontalAlignment='Center' VerticalAlignment='Center'", "HorizontalAlignment='Right' VerticalAlignment='Bottom'" })
        foreach (var name in new[] { "Left", "Top", "Right", "Bottom" })
        {
            var fixture = source.Replace("Canvas>", "Grid>")
                .Replace("Canvas.Left='40' Canvas.Top='50'", "Margin='10,20,30,40' " + alignment);
            await Load(fixture);
            var before = Selected().Bounds;
            Drag("SplitResize" + name, name is "Left" or "Right" ? new Vector(10,0) : new Vector(0,10));
            await Task.Delay(750);
            var after = Selected().Bounds;
            Check(Near(after.Width, before.Width + (name == "Left" ? -10 : name == "Right" ? 10 : 0)) &&
                  Near(after.Height, before.Height + (name == "Top" ? -10 : name == "Bottom" ? 10 : 0)) &&
                  Near(name == "Left" ? after.Right : after.X, name == "Left" ? before.Right : before.X) &&
                  Near(name == "Top" ? after.Bottom : after.Y, name == "Top" ? before.Bottom : before.Y),
                "Grid " + alignment + " resize " + name + " keeps opposite edge fixed");
        }
        Console.WriteLine("TOTAL INTERACTION FAILURES: " + failures);
        if (failures > 0) throw new Exception("Interaction checks failed: " + failures);
    }
}
