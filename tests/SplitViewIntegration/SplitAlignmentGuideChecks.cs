using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.Designer;

internal static class SplitAlignmentGuideChecks
{
    public static async Task Run(Document doc, DocumentView view)
    {
        var failures = 0;
        void Check(bool ok, string message)
        {
            Console.WriteLine((ok ? "PASS: " : "FAIL: ") + message);
            if (!ok) failures++;
        }

        var source = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "TestAlignmentGuides.axaml"));
        var editor = view.GetVisualDescendants().OfType<XamlEditorView>().Single().Editor!;
        var handles = view.FindControl<Canvas>("SplitResizeOverlay")!;
        var guides = view.FindControl<Canvas>("AlignmentGuidesOverlay")!;
        var selectionOverlay = view.FindControl<Border>("PreviewSelectionOverlay")!;
        var service = doc.DesignContext!.Services.GetService<ISplitResizeOverlayService>()!;

        async Task Select(string text, string marker)
        {
            editor.Text = text;
            await Task.Delay(750);
            editor.Select(text.IndexOf(marker, StringComparison.Ordinal), 0);
            await Task.Delay(150);
            editor.Document.UndoStack.ClearAll();
        }

        Button Selected() => (Button)doc.SelectionService!.PrimarySelection!.Component;
        Control Handle(string name) => handles.Children.OfType<Control>().Single(c => c.Name == name);
        Rect Bounds(Control control)
        {
            var topLeft = control.TranslatePoint(default, handles)!.Value;
            return new Rect(topLeft, control.Bounds.Size);
        }

        void Drag(string name, Vector delta, KeyModifiers modifiers, Action during)
        {
            var handle = Handle(name);
            var root = (Visual)TopLevel.GetTopLevel(handle)!;
            var start = handle.TranslatePoint(new Point(4, 4), root)!.Value;
            var selected = Selected();
            var screenDelta = selected.TranslatePoint(new Point(delta.X, delta.Y), root)!.Value
                - selected.TranslatePoint(default, root)!.Value;
            using var pointer = new Pointer(71, PointerType.Mouse, true);
            handle.RaiseEvent(new PointerPressedEventArgs(handle, pointer, root, start, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), modifiers, 1));
            handle.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, handle, pointer, root, start + screenDelta, 1,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), modifiers));
            during();
            handle.RaiseEvent(new PointerReleasedEventArgs(handle, pointer, root, start + screenDelta, 2,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), modifiers, MouseButton.Left));
        }

        async Task TestMoveGuide(string handleName)
        {
            await Select(source, "DRAG ME");
            service.SnapEnabled = false;
            var moving = Selected();
            var reference = doc.DesignSurface.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Ref Left-Top"));
            var delta = Bounds(reference).Position - Bounds(moving).Position;
            var before = doc.Text;
            Drag(handleName, delta, KeyModifiers.None, () => Check(guides.Children.Count > 0, handleName + " shows guide at aligned bounds"));
            Check(guides.Children.Count == 0, handleName + " hides guides on release");
            await Task.Delay(750);
            Check(doc.Text != before, handleName + " updates XAML position");
        }

        await TestMoveGuide("SplitMoveHandle");
        await TestMoveGuide("SplitBorderDrag");

        await Select(source, "DRAG ME");
        service.SnapEnabled = false;
        var dragButton = Selected();
        var refButton = doc.DesignSurface.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Ref Left-Top"));
        var resizeDelta = new Vector(Bounds(refButton).Right - Bounds(dragButton).Right, 0);
        Drag("SplitResizeRight", resizeDelta, KeyModifiers.None,
            () => Check(guides.Children.Count > 0, "Resize handle shows width/edge alignment guide"));

        await Select(source, "DRAG ME");
        service.SnapEnabled = true;
        dragButton = Selected();
        refButton = doc.DesignSurface.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Ref Left-Top"));
        var altDelta = Bounds(refButton).Position - Bounds(dragButton).Position;
        Drag("SplitMoveHandle", altDelta, KeyModifiers.Alt,
            () => Check(guides.Children.Count == 0, "Alt drag hides alignment guides"));

        await Select(source, "DRAG ME (Stretch)");
        var gridTabs = doc.DesignSurface.GetVisualDescendants().OfType<TabControl>().First();
        gridTabs.SelectedIndex = 1;
        await Task.Delay(150);
        editor.Select(source.IndexOf("DRAG ME (Stretch)", StringComparison.Ordinal), 0);
        await Task.Delay(150);
        service.SnapEnabled = false;
        var stretchBefore = Selected().Margin;
        Drag("SplitMoveHandle", new Vector(8, 8), KeyModifiers.None, () => { });
        await Task.Delay(750);
        var stretchAfter = Selected().Margin;
        Check(stretchAfter.Left == stretchBefore.Left + 8 && stretchAfter.Top == stretchBefore.Top + 8
            && stretchAfter.Right == stretchBefore.Right && stretchAfter.Bottom == stretchBefore.Bottom,
            "Grid Stretch move updates leading margins and preserves trailing margins");

        var keyboardSource = source.Replace("Canvas.Left=\"200\" Canvas.Top=\"200\" Content=\"DRAG ME\"",
            "Canvas.Left=\"99\" Canvas.Top=\"100\" Content=\"DRAG ME\"");
        await Select(keyboardSource, "DRAG ME");
        service.SnapEnabled = false;
        handles.Focus();
        handles.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
        Check(guides.Children.Count > 0, "Arrow key shows guide when reaching alignment");
        await Task.Delay(750);
        Check(doc.Text.Contains("Canvas.Left=\"100\""), "Arrow key updates XAML by 1px");

        await Select(source, "Multi-1");
        var tabs = doc.DesignSurface.GetVisualDescendants().OfType<TabControl>().First();
        tabs.SelectedIndex = 2;
        await Task.Delay(150);
        var multi1 = doc.DesignSurface.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Multi-1"));
        var multi2 = doc.DesignSurface.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Multi-2"));
        void Click(Control target, KeyModifiers modifiers)
        {
            var point = target.TranslatePoint(new Point(4, 4), selectionOverlay)!.Value;
            using var pointer = new Pointer(72, PointerType.Mouse, true);
            selectionOverlay.RaiseEvent(new PointerPressedEventArgs(selectionOverlay, pointer, selectionOverlay, point, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), modifiers, 1));
        }
        Click(multi1, KeyModifiers.None);
        Click(multi2, KeyModifiers.Control);
        Check(doc.SelectionService!.SelectionCount == 2, "Ctrl+Click creates a two-control selection");

        Console.WriteLine("TOTAL ALIGNMENT GUIDE FAILURES: " + failures);
        if (failures > 0) throw new Exception("Alignment guide checks failed: " + failures);
    }
}
