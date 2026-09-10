using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.Designer;

internal static class SplitResizeChecks
{
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }

    public static async Task Run(Document doc, DocumentView view)
    {
        var editor = view.GetVisualDescendants().OfType<XamlEditorView>().Single().Editor!;
        var overlay = view.FindControl<Canvas>("SplitResizeOverlay")!;
        doc.DesignContext!.Services.GetService<Scadix.AxamlDesign.ISplitResizeOverlayService>()!.SnapEnabled = false;
        const string source = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\">\r\n<!-- keep -->\r\n<Canvas><Button Content='Resize' Width = '120' Height='60' /></Canvas></UserControl>";
        async Task Load(string text)
        {
            editor.Text = text;
            if (doc.Text != text) throw new Exception("FAIL: Editor remains connected to document after reattachment");
            await Task.Delay(750);
            editor.Select(text.IndexOf("<Button", StringComparison.Ordinal) + 2, 0);
            await Task.Delay(100);
        }
        Control Handle(string name) => overlay.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);
        void Drag(string name, Vector delta, KeyModifiers modifiers = KeyModifiers.None, bool cancel = false)
        {
            var handle = Handle(name);
            var root = (Visual)TopLevel.GetTopLevel(handle)!;
            var start = handle.TranslatePoint(new Point(4, 4), root)!.Value;
            using var pointer = new Pointer(9, PointerType.Mouse, true);
            handle.RaiseEvent(new PointerPressedEventArgs(handle, pointer, root, start, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), modifiers, 1));
            handle.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, handle, pointer, root, start + delta, 1,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), modifiers));
            if (cancel) handle.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            handle.RaiseEvent(new PointerReleasedEventArgs(handle, pointer, root, start + delta, 2,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), modifiers, MouseButton.Left));
        }
        await Load(source);
        Check(overlay.Children.OfType<Control>().Count(c => c.Name?.StartsWith("SplitResize") == true) == 8, "Split provides eight resize handles on the interactive overlay after reload");
        var corner = Handle("SplitResizeBottomRight");
        Check(corner.IsEffectivelyEnabled && corner.IsEffectivelyVisible, "Resize handle is enabled and visible");
        var center = corner.TranslatePoint(new Point(4, 4), view)!.Value;
        Check(ReferenceEquals(view.InputHitTest(center), corner), "Actual hit testing reaches the resize handle");
        Drag("SplitResizeBottomRight", new Vector(20, 10));
        var resized = source.Replace("'120'", "'140'").Replace("'60'", "'70'");
        Check(doc.Text == resized, "Resize patches both dimensions and preserves exact surrounding XAML" +
            (doc.Text == resized ? "" : "\nActual: " + doc.Text + "\nExpected: " + resized));
        await Task.Delay(750);
        Check(doc.SelectionService?.PrimarySelection?.Component is Button b && b.Width == 140 && b.Height == 70, "Resize refresh retains selected control");
        doc.UndoCommand.Execute(null);
        Check(doc.Text == source, "One Undo restores both resize dimensions");
        await Task.Delay(750);
        doc.RedoCommand.Execute(null);
        Check(doc.Text == resized, "Redo restores the resize");
        await Load(source);
        Drag("SplitResizeBottomRight", new Vector(20, 3), KeyModifiers.Control);
        Check(doc.Text == resized, "Ctrl corner resize preserves aspect ratio");
        await Load(source);
        Drag("SplitResizeRight", new Vector(20, 25));
        Check(doc.Text == source.Replace("'120'", "'140'"), "Edge resize changes only its own dimension");
        await Load(source);
        Drag("SplitResizeTopLeft", new Vector(-20, -10));
        Check(doc.Text == resized.Replace("<Button", "<Button Canvas.Top=\"-10\" Canvas.Left=\"-20\""), "Top-left resize updates dimensions and position (actual: " + doc.Text + ")");
        await Load(source);
        Drag("SplitResizeBottomRight", new Vector(20, 10), cancel: true);
        Check(doc.Text == source && overlay.Children.OfType<Control>().Count(c => c.Name?.StartsWith("SplitResize") == true) == 8, "Escape cancels resize and retains handles");
        editor.Document.UndoStack.ClearAll();
        Drag("SplitResizeBottomRight", default);
        Check(doc.Text == source && !editor.Document.UndoStack.CanUndo, "Click without dragging creates no source undo entry");
        foreach (var (name, delta, expected) in new[]
        {
            ("Top", new Vector(0, -10), source.Replace("'60'", "'70'").Replace("<Button", "<Button Canvas.Top=\"-10\"")),
            ("Bottom", new Vector(0, 10), source.Replace("'60'", "'70'")),
            ("Left", new Vector(-20, 0), source.Replace("'120'", "'140'").Replace("<Button", "<Button Canvas.Left=\"-20\"")),
            ("TopRight", new Vector(20, -10), resized.Replace("<Button", "<Button Canvas.Top=\"-10\"")),
            ("BottomLeft", new Vector(-20, 10), resized.Replace("<Button", "<Button Canvas.Left=\"-20\""))
        })
        {
            await Load(source);
            Drag("SplitResize" + name, delta);
            Check(doc.Text == expected, name + " handle applies correct dimensions");
        }
        await Load(source);
        var button = (Button)doc.SelectionService!.PrimarySelection!.Component;
        button.RenderTransform = new Avalonia.Media.ScaleTransform(1.5, 1.5);
        await Task.Delay(100);
        corner = Handle("SplitResizeBottomRight");
        var actualCorner = corner.TranslatePoint(new Point(4, 4), view)!.Value;
        var expectedCorner = button.TranslatePoint(new Point(120, 60), view)!.Value;
        Check(Math.Abs(actualCorner.X - expectedCorner.X) < 1 && Math.Abs(actualCorner.Y - expectedCorner.Y) < 1,
            "Handles follow transformed preview coordinates");
        var window = (Visual)TopLevel.GetTopLevel(button)!;
        var origin = button.TranslatePoint(default, window)!.Value;
        var moved = button.TranslatePoint(new Point(20, 10), window)!.Value;
        Drag("SplitResizeBottomRight", new Vector(moved.X - origin.X, moved.Y - origin.Y));
        Check(doc.Text == resized, "Resize converts scaled pointer movement into control dimensions" +
            (doc.Text == resized ? "" : "\nActual: " + doc.Text));
        await Load(source.Replace("Width = '120'", "Width = '120' MinWidth='100'"));
        Drag("SplitResizeRight", new Vector(-80, 0));
        Check(doc.Text.Contains("Width = '100'"), "Resize respects minimum width");
        await Load(source.Replace(" Width = '120' Height='60'", ""));
        button = (Button)doc.SelectionService!.PrimarySelection!.Component;
        var measured = button.Bounds.Size;
        Drag("SplitResizeBottomRight", new Vector(20, 10));
        var element = System.Xml.Linq.XDocument.Parse(doc.Text).Descendants().Single(e => e.Name.LocalName == "Button");
        Check(Math.Abs((double)element.Attribute("Width")! - measured.Width - 20) < .001 &&
              Math.Abs((double)element.Attribute("Height")! - measured.Height - 10) < .001,
              "Resize inserts missing Width and Height together");
        doc.Save();
        Check(File.ReadAllText(doc.FilePath) == doc.Text && !doc.IsDirty, "Saving resize writes the exact edited source");
        await Load(source.Replace("Width = '120'", "Width = '{Binding Size}'"));
        Check(!overlay.Children.OfType<Control>().Any(c => c.Name?.StartsWith("SplitResize") == true), "Bound dimensions are protected from resize");
        await Load(source);
        editor.Text = "<UserControl";
        Check(overlay.Children.Count == 0, "Editing source immediately removes stale resize handles");
        await Load(source);
        doc.Mode = DocumentMode.Xaml;
        Check(overlay.Children.Count == 0, "Leaving Split removes resize handles");
    }
}
