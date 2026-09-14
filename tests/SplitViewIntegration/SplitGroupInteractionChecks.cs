using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.Designer;

internal static class SplitGroupInteractionChecks
{
    public static async Task Run(Document doc, DocumentView view, bool resizeOnly = false)
    {
        if (doc.Mode != DocumentMode.Split)
        {
            doc.Mode = DocumentMode.Split;
            await Task.Delay(100);
        }
        var failures = 0;
        void Check(bool ok, string message)
        {
            Console.WriteLine((ok ? "PASS: " : "FAIL: ") + message);
            if (!ok) failures++;
        }
        bool Near(double a, double b) => Math.Abs(a - b) < .1;
        var editor = view.GetVisualDescendants().OfType<XamlEditorView>().Single().Editor!;
        var overlay = view.FindControl<Canvas>("SplitResizeOverlay")!;
        var selectionOverlay = view.FindControl<Border>("PreviewSelectionOverlay")!;
        var guides = view.FindControl<Canvas>("AlignmentGuidesOverlay")!;
        var readout = view.FindControl<Border>("SnapReadout")!;
        var readoutText = view.FindControl<TextBlock>("SnapReadoutText")!;
        var service = doc.DesignContext!.Services.GetService<ISplitResizeOverlayService>()!;
        const string source = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'>\r\n<!-- keep -->\r\n<Canvas><Button Content='A' Width='40' Height='24' Canvas.Left='40' Canvas.Top='48' /><Button Content='B' Width='32' Height='24' Canvas.Left='112' Canvas.Top='80' /><Button Content='Reference' Width='56' Height='64' Canvas.Left='240' Canvas.Top='48' /></Canvas></UserControl>";
        Button Button(string name) => doc.DesignSurface.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, name));
        Control? Handle(string name) => overlay.Children.OfType<Control>().SingleOrDefault(c => c.Name == name && c.IsVisible);
        void Click(Control target, KeyModifiers modifiers = KeyModifiers.None, bool hitTest = false)
        {
            var point = target.TranslatePoint(new Point(4, 4), selectionOverlay)!.Value;
            var receiver = hitTest
                ? (Control)view.InputHitTest(target.TranslatePoint(new Point(4, 4), view)!.Value)!
                : selectionOverlay;
            using var pointer = new Pointer(91, PointerType.Mouse, true);
            receiver.RaiseEvent(new PointerPressedEventArgs(receiver, pointer, selectionOverlay, point, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), modifiers, 1));
        }
        async Task Load(string text = source)
        {
            editor.Text = text;
            await Task.Delay(750);
            Click(Button("A"));
            Click(Button("B"), KeyModifiers.Control);
            await Task.Delay(100);
            editor.Document.UndoStack.ClearAll();
            service.SnapEnabled = true;
            service.SnapGridSize = 8;
        }
        void Key(Key key, KeyModifiers modifiers = KeyModifiers.None)
        {
            var focused = TopLevel.GetTopLevel(view)!.FocusManager!.GetFocusedElement() as InputElement;
            focused?.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });
        }
        void Drag(string name, Vector delta, KeyModifiers modifiers = KeyModifiers.None, bool cancel = false, Action? during = null)
        {
            var handle = Handle(name);
            Check(handle != null, name + " exists for dragging");
            if (handle == null) return;
            var root = (Visual)TopLevel.GetTopLevel(handle)!;
            var start = handle.TranslatePoint(new Point(4, 4), root)!.Value;
            var parent = Button("A").GetVisualParent()!;
            var screenDelta = parent.TranslatePoint(new Point(delta.X, delta.Y), root)!.Value - parent.TranslatePoint(default, root)!.Value;
            using var pointer = new Pointer(92, PointerType.Mouse, true);
            handle.RaiseEvent(new PointerPressedEventArgs(handle, pointer, root, start, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), modifiers, 1));
            handle.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, handle, pointer, root, start + screenDelta, 1,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), modifiers));
            during?.Invoke();
            if (cancel) Key(Avalonia.Input.Key.Escape);
            handle.RaiseEvent(new PointerReleasedEventArgs(handle, pointer, root, start + screenDelta, 2,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), modifiers, MouseButton.Left));
        }
        Pointer BeginGroupDrag(string name, Vector delta)
        {
            var handle = Handle(name);
            Check(handle != null, name + " exists for lifecycle cancellation");
            if (handle == null) throw new InvalidOperationException(name + " is unavailable");
            var root = (Visual)TopLevel.GetTopLevel(handle)!;
            var start = handle.TranslatePoint(new Point(4, 4), root)!.Value;
            var parent = Button("A").GetVisualParent()!;
            var screenDelta = parent.TranslatePoint(new Point(delta.X, delta.Y), root)!.Value - parent.TranslatePoint(default, root)!.Value;
            var pointer = new Pointer(93, PointerType.Mouse, true);
            handle.RaiseEvent(new PointerPressedEventArgs(handle, pointer, root, start, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
            handle.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, handle, pointer, root, start + screenDelta, 1,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
            return pointer;
        }
        bool NoGroupGestureFeedback() => !overlay.Children.OfType<Control>().Any(c => c.IsVisible && c.Name?.StartsWith("SplitGroup") == true)
            && guides.Children.Count == 0 && !readout.IsVisible;
        void Positions(double ax, double ay, double bx, double by, string label)
            => Check(Near(Button("A").Bounds.X, ax) && Near(Button("A").Bounds.Y, ay)
                && Near(Button("B").Bounds.X, bx) && Near(Button("B").Bounds.Y, by), label);

        void Bounds(Rect a, Rect b, string label)
        {
            var actualA = Button("A").Bounds;
            var actualB = Button("B").Bounds;
            bool Matches(Rect actual, Rect expected) => Near(actual.X, expected.X) && Near(actual.Y, expected.Y)
                && Near(actual.Width, expected.Width) && Near(actual.Height, expected.Height);
            Check(Matches(actualA, a) && Matches(actualB, b), label + $" (A: {actualA}; B: {actualB})");
        }
        // Check fractional transforms independently of Avalonia's pixel rounding at arrange time.
        var resizeSource = source.Replace("<UserControl", "<UserControl UseLayoutRounding='False'");
        foreach (var (direction, delta, a, b) in new[]
        {
            ("TopLeft", new Vector(-104, -56), new Rect(-64, -8, 80, 48), new Rect(80, 56, 64, 48)),
            ("Top", new Vector(17, -56), new Rect(40, -8, 40, 48), new Rect(112, 56, 32, 48)),
            ("TopRight", new Vector(104, -56), new Rect(40, -8, 80, 48), new Rect(184, 56, 64, 48)),
            ("Left", new Vector(-104, 17), new Rect(-64, 48, 80, 24), new Rect(80, 80, 64, 24)),
            ("Right", new Vector(104, 17), new Rect(40, 48, 80, 24), new Rect(184, 80, 64, 24)),
            ("BottomLeft", new Vector(-104, 56), new Rect(-64, 48, 80, 48), new Rect(80, 112, 64, 48)),
            ("Bottom", new Vector(17, 56), new Rect(40, 48, 40, 48), new Rect(112, 112, 32, 48)),
            ("BottomRight", new Vector(104, 56), new Rect(40, 48, 80, 48), new Rect(184, 112, 64, 48))
        })
        {
            await Load(resizeSource);
            var original = doc.Text;
            var first = Button("A");
            var second = Button("B");
            var beforeA = first.Bounds;
            var beforeB = second.Bounds;
            var name = "SplitGroupResize" + direction;
            Check(overlay.Children.OfType<Control>().Count(c => c.IsVisible && c.Name?.StartsWith("SplitGroupResize") == true) == 8,
                direction + " has exactly eight group resize handles");
            Drag(name, delta, during: () =>
            {
                Check(doc.Text == original && first.Bounds == beforeA && second.Bounds == beforeB
                    && first.Width == 40 && second.Width == 32 && !editor.Document.UndoStack.CanUndo,
                    name + " previews without source or runtime mutation");
                var union = a.Union(b);
                var preview = Handle("SplitGroupBorderDrag")!;
                var point = first.GetVisualParent()!.TranslatePoint(union.Position, overlay)!.Value;
                Check(Near(Canvas.GetLeft(preview), point.X) && Near(Canvas.GetTop(preview), point.Y)
                    && Near(preview.Width, union.Width) && Near(preview.Height, union.Height), name + " previews constrained union");
                Check(readout.IsVisible, name + " displays a size readout");
            });
            await Task.Delay(100);
            Bounds(a, b, name + " proportionally transforms every child and fixes the opposite edge");
            Check(!readout.IsVisible && guides.Children.Count == 0, name + " clears feedback");
            Check(doc.SelectionService!.SelectionCount == 2 && Equals(((Button)doc.SelectionService.PrimarySelection.Component).Content, "A"),
                name + " retains group and primary");
            var changed = doc.Text;
            Check(changed != original, name + " commits on release");
            doc.UndoCommand.Execute(null);
            Check(doc.Text == original && !editor.Document.UndoStack.CanUndo, name + " uses one Undo entry");
            await Task.Delay(750);
            doc.RedoCommand.Execute(null);
            Check(doc.Text == changed, name + " supports one-step Redo");
            await Task.Delay(750);
        }

        await Load(resizeSource);
        var resizeOriginal = doc.Text;
        Drag("SplitGroupResizeTopLeft", new Vector(-104, -56), cancel: true, during: () => Key(Avalonia.Input.Key.Right));
        Check(doc.Text == resizeOriginal && !editor.Document.UndoStack.CanUndo, "Escape cancels group resize and drag blocks arrow commits");
        Bounds(new Rect(40, 48, 40, 24), new Rect(112, 80, 32, 24), "Escape preserves all child bounds");
        Check(!readout.IsVisible && guides.Children.Count == 0, "Escape clears group resize feedback");
        Drag("SplitGroupResizeBottomRight", default);
        Check(doc.Text == resizeOriginal && !editor.Document.UndoStack.CanUndo, "No-op group resize writes nothing");

        // An off-grid inactive axis must retain its exact original dimensions and position.
        var offGrid = resizeSource.Replace("Height='24'", "Height='25'").Replace("Canvas.Top='48'", "Canvas.Top='49'");
        await Load(offGrid);
        Drag("SplitGroupResizeRight", new Vector(11, 31));
        Bounds(new Rect(40, 49, 43.08, 25), new Rect(117.54, 80, 34.46, 25), "Group resize snaps only the active union edge");
        await Load(offGrid);
        Drag("SplitGroupResizeTop", new Vector(31, -11));
        Bounds(new Rect(40, 40, 40, 29.02), new Rect(112, 75.98, 32, 29.02), "Top resize snaps active edge and fixes opposite bottom");
        await Load(resizeSource);
        Drag("SplitGroupResizeRight", new Vector(11, 31), KeyModifiers.Alt,
            during: () => Check(guides.Children.Count == 0, "Alt hides group resize guides"));
        Bounds(new Rect(40, 48, 44.23, 24), new Rect(119.62, 80, 35.38, 24), "Alt bypasses group resize snapping");

        await Load(resizeSource.Replace("Content='B'", "Content='B' MinWidth='24' MinHeight='18'"));
        Drag("SplitGroupResizeTopLeft", new Vector(300, 300));
        Bounds(new Rect(66, 62, 30, 18), new Rect(120, 86, 24, 18), "All child minima clamp scaling after snap and fix opposite corner");
        await Load(resizeSource.Replace("Content='B'", "Content='B' MaxWidth='48' MaxHeight='36'"));
        Drag("SplitGroupResizeTopLeft", new Vector(-300, -300));
        Bounds(new Rect(-12, 20, 60, 36), new Rect(96, 68, 48, 36), "All child maxima clamp scaling and fix opposite corner");

        await Load(source.Replace("Content='A'", "Content='A' MinWidth='40.1' MaxWidth='40.1'")
            .Replace("Content='B'", "Content='B' MinWidth='32.1' MaxWidth='32.1'"));
        var constrainedSource = doc.Text;
        Drag("SplitGroupResizeRight", new Vector(104, 0));
        Check(doc.Text == constrainedSource && !editor.Document.UndoStack.CanUndo,
            "Incompatible child scale constraints reject atomically instead of violating a maximum");

        await Load(resizeSource.Replace("Width='32'", "Width='{Binding ProtectedWidth}'"));
        var protectedSource = doc.Text;
        Check(Handle("SplitGroupResizeRight") == null, "Protected child size rejects all group resize handles");
        Check(doc.Text == protectedSource && !editor.Document.UndoStack.CanUndo, "Protected child resize rejection is atomic");

        foreach (var (label, fixture, direction, delta, expectedUnion, expectedA, expectedB, enabled) in new[]
        {
            ("Zero-width union", resizeSource.Replace("Width='40'", "Width='0'").Replace("Width='32'", "Width='0'")
                .Replace("Canvas.Left='112'", "Canvas.Left='40'"), "Top", new Vector(31, -56),
                new Rect(40, -8, 0, 112), new Rect(40, -8, 0, 48), new Rect(40, 56, 0, 48), new[] { "Top", "Bottom" }),
            ("Zero-height union", resizeSource.Replace("Height='24'", "Height='0'").Replace("Canvas.Top='80'", "Canvas.Top='48'"),
                "Left", new Vector(-104, 31), new Rect(-64, 48, 208, 0), new Rect(-64, 48, 80, 0), new Rect(80, 48, 64, 0),
                new[] { "Left", "Right" }),
            ("Zero-sized child in nondegenerate union", resizeSource.Replace("Content='B' Width='32' Height='24' Canvas.Left='112' Canvas.Top='80'",
                "Content='B' Width='0' Height='0' MaxWidth='0' MaxHeight='0' Canvas.Left='56' Canvas.Top='56'"),
                "BottomRight", new Vector(40, 24), new Rect(40, 48, 80, 48), new Rect(40, 48, 80, 48), new Rect(72, 64, 0, 0),
                new[] { "TopLeft", "Top", "TopRight", "Left", "Right", "BottomLeft", "Bottom", "BottomRight" })
        })
        {
            await Load(fixture);
            // Zero-sized controls are selected through the same service used by the outline.
            var components = doc.DesignContext!.Services.Component;
            doc.SelectionService!.SetSelectedComponents(new[] { components.GetDesignItem(Button("A")), components.GetDesignItem(Button("B")) }, SelectionTypes.Replace);
            var first = Button("A");
            var second = Button("B");
            var beforeA = first.Bounds;
            var beforeB = second.Bounds;
            var original = doc.Text;
            Check(doc.SelectionService.SelectionCount == 2, label + " selects both children");
            Check(overlay.Children.OfType<Control>().Where(c => c.IsVisible && c.Name?.StartsWith("SplitGroupResize") == true)
                .Select(c => c.Name!["SplitGroupResize".Length..]).OrderBy(n => n).SequenceEqual(enabled.OrderBy(n => n)),
                label + " disables only handles that use a degenerate union axis");
            Drag("SplitGroupResize" + direction, delta, cancel: true);
            Check(doc.Text == original && !editor.Document.UndoStack.CanUndo, label + " Escape writes nothing");
            Drag("SplitGroupResize" + direction, delta, during: () =>
            {
                Check(doc.Text == original && first.Bounds == beforeA && second.Bounds == beforeB && !editor.Document.UndoStack.CanUndo,
                    label + " preview preserves source and runtime bounds");
                var preview = Handle("SplitGroupBorderDrag")!;
                var position = first.GetVisualParent()!.TranslatePoint(expectedUnion.Position, overlay)!.Value;
                Check(Near(Canvas.GetLeft(preview), position.X) && Near(Canvas.GetTop(preview), position.Y)
                    && Near(preview.Width, expectedUnion.Width) && Near(preview.Height, expectedUnion.Height),
                    label + " previews the proportional union with its opposite edge fixed");
            });
            Bounds(expectedA, expectedB, label + " scales every child on the valid axis and retains zero extents");
            var changed = doc.Text;
            Check(changed != original, label + " commits the whole group on release");
            doc.UndoCommand.Execute(null);
            Check(doc.Text == original && !editor.Document.UndoStack.CanUndo, label + " is one Undo entry");
            await Task.Delay(750);
            doc.RedoCommand.Execute(null);
            Check(doc.Text == changed, label + " supports one-step Redo");
            await Task.Delay(750);
        }
        if (resizeOnly)
        {
            Console.WriteLine("TOTAL GROUP RESIZE FAILURES: " + failures);
            if (failures > 0) throw new Exception("Group resize checks failed: " + failures);
            return;
        }

        await Load();
        Check(doc.SelectionService!.SelectionCount == 2, "Ctrl+Click selects two siblings");
        var primary = doc.SelectionService.PrimarySelection;
        Check(ReferenceEquals(primary.Component, Button("A")), "Ctrl+Click addition preserves the primary");
        var border = Handle("SplitGroupBorderDrag");
        Check(border != null && Handle("SplitGroupMoveHandle") != null, "Eligible selection shows one union border and move handle");
        if (border != null)
        {
            var expectedPosition = Button("A").GetVisualParent()!.TranslatePoint(new Point(40, 48), overlay)!.Value;
            Check(Near(Canvas.GetLeft(border), expectedPosition.X) && Near(Canvas.GetTop(border), expectedPosition.Y)
                && Near(border.Width, 104) && Near(border.Height, 56), "Group border encloses the parent-space union");
        }
        Check(!overlay.Children.OfType<Control>().Any(c => c.IsVisible && (c.Name?.StartsWith("SplitResize") == true
            || c.Name == "SplitMoveHandle" || c.Name == "SplitBorderDrag")), "Group selection hides individual editing handles");
        Click(Button("B"), KeyModifiers.Control, hitTest: true);
        Check(doc.SelectionService.SelectionCount == 1 && ReferenceEquals(primary, doc.SelectionService.PrimarySelection), "Ctrl+Click removal preserves remaining primary");
        Check(Handle("SplitGroupBorderDrag") == null && Handle("SplitMoveHandle") != null, "Removing a sibling restores individual handles");
        Click(Button("B"), KeyModifiers.Control);
        Click(Button("A"), KeyModifiers.Control, hitTest: true);
        Check(doc.SelectionService.SelectionCount == 1 && ReferenceEquals(doc.SelectionService.PrimarySelection.Component, Button("B")), "Removing primary promotes remaining item");

        foreach (var name in new[] { "SplitGroupMoveHandle", "SplitGroupBorderDrag" })
        {
            await Load();
            var original = doc.Text;
            var first = Button("A");
            var second = Button("B");
            var aBounds = first.Bounds;
            var bBounds = second.Bounds;
            Drag(name, new Vector(16, 8), during: () =>
            {
                Check(doc.Text == original && first.Bounds == aBounds && second.Bounds == bBounds
                    && Canvas.GetLeft(first) == 40 && Canvas.GetLeft(second) == 112,
                    name + " preview leaves source and runtime controls unchanged");
                var preview = Handle("SplitGroupBorderDrag")!;
                var point = first.GetVisualParent()!.TranslatePoint(new Point(56, 56), overlay)!.Value;
                Check(Near(Canvas.GetLeft(preview), point.X) && Near(Canvas.GetTop(preview), point.Y), name + " previews translated union");
                Check(guides.Children.Count > 0, name + " shows union alignment guides");
                Check(readout.IsVisible && readoutText.Text == "X: 56  Y: 56", name + " shows union position readout");
            });
            await Task.Delay(100);
            Positions(56, 56, 128, 88, name + " moves every sibling by 16,8");
            Check(!readout.IsVisible && guides.Children.Count == 0, name + " clears feedback after release");
            Check(doc.SelectionService!.SelectionCount == 2 && Equals(((Button)doc.SelectionService.PrimarySelection.Component).Content, "A"), name + " preserves group and primary after refresh");
            var changed = doc.Text;
            doc.UndoCommand.Execute(null);
            Check(doc.Text == original && !editor.Document.UndoStack.CanUndo, name + " is one Undo entry");
            await Task.Delay(750);
            doc.RedoCommand.Execute(null);
            Check(doc.Text == changed, name + " supports one-step Redo");
            await Task.Delay(750);
        }

        await Load();
        Drag("SplitGroupMoveHandle", new Vector(11, 5));
        await Task.Delay(100);
        Positions(48, 56, 120, 88, "Pointer movement snaps union position to the 8 px grid");
        await Load();
        Drag("SplitGroupMoveHandle", new Vector(11, 5), KeyModifiers.Alt,
            during: () => Check(guides.Children.Count == 0, "Alt bypass hides group guides"));
        await Task.Delay(100);
        Positions(51, 53, 123, 85, "Alt bypass preserves exact pointer delta");

        await Load();
        var cancelledSource = doc.Text;
        Drag("SplitGroupBorderDrag", new Vector(16, 8), cancel: true,
            during: () => Key(Avalonia.Input.Key.Right));
        Check(doc.Text == cancelledSource && !editor.Document.UndoStack.CanUndo, "Escape cancels the whole gesture and arrows cannot commit during drag");
        Check(!readout.IsVisible && guides.Children.Count == 0, "Escape clears union readout and guides");
        Positions(40, 48, 112, 80, "Escape leaves both runtime positions unchanged");

        await Load();
        using (BeginGroupDrag("SplitGroupMoveHandle", new Vector(16, 8)))
        {
            var typedSource = source.Replace("Content='Reference'", "Content='Typed source'");
            editor.Text = typedSource;
            Check(doc.Text == typedSource && NoGroupGestureFeedback(),
                "Typing cancels an active group gesture and clears feedback");
        }
        await Task.Delay(750);

        await Load();
        using (BeginGroupDrag("SplitGroupMoveHandle", new Vector(16, 8)))
        {
            doc.Refresh();
            await Task.Delay(100);
            Check(NoGroupGestureFeedback(), "Preview reload cancels an active group gesture and clears feedback");
        }
        await Task.Delay(750);

        await Load();
        using (BeginGroupDrag("SplitGroupMoveHandle", new Vector(16, 8)))
        {
            doc.Mode = DocumentMode.Xaml;
            await Task.Delay(100);
            Check(NoGroupGestureFeedback(), "Mode change cancels an active group gesture and clears feedback");
            doc.Mode = DocumentMode.Split;
        }
        await Task.Delay(750);

        await Load();
        using (BeginGroupDrag("SplitGroupMoveHandle", new Vector(16, 8)))
        {
            var host = (Window)TopLevel.GetTopLevel(view)!;
            host.Content = null;
            await Task.Delay(100);
            Check(NoGroupGestureFeedback(), "Document detach cancels an active group gesture and clears feedback");
            host.Content = view;
        }
        await Task.Delay(750);

        await Load();
        overlay.Focus();
        Key(Avalonia.Input.Key.Right);
        Positions(41, 48, 113, 80, "Arrow moves every item by 1 px without grid snapping");
        Check(guides.Children.Count > 0, "Keyboard movement shows union guides after refresh");
        Key(Avalonia.Input.Key.Down, KeyModifiers.Shift);
        Positions(41, 58, 113, 90, "Shift+Arrow moves every item by 10 px");
        Key(Avalonia.Input.Key.Left);
        Key(Avalonia.Input.Key.Up, KeyModifiers.Shift);
        Positions(40, 48, 112, 80, "Repeated keyboard edits preserve group and primary");

        await Load();
        Click(Button("B"));
        Click(Button("A"), KeyModifiers.Control);
        Drag("SplitGroupMoveHandle", new Vector(16, 8));
        Positions(56, 56, 128, 88, "Reverse selection order maps each committed bound to its original item");
        Check(doc.SelectionService!.SelectedItems.Select(item => ((Button)item.Component).Content).SequenceEqual(new[] { "B", "A" })
            && Equals(((Button)doc.SelectionService.PrimarySelection.Component).Content, "B"), "Refresh preserves selection order and the later source primary");

        const string grid = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'><Grid><Button Content='A' Width='40' Height='24' HorizontalAlignment='Left' VerticalAlignment='Top' Margin='40,48,0,0' /><Button Content='B' Width='32' Height='24' HorizontalAlignment='Right' VerticalAlignment='Bottom' Margin='0,0,40,48' /></Grid></UserControl>";
        await Load(grid);
        Drag("SplitGroupMoveHandle", new Vector(16, 8));
        await Task.Delay(100);
        Check(Button("A").Margin == new Thickness(56, 56, 0, 0) && Button("B").Margin == new Thickness(0, 0, 24, 40), "Group movement commits Grid siblings with opposite anchors");

        foreach (var (text, label) in new[]
        {
            ("<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'><StackPanel><Button Content='A' Width='40' Height='24' /><Button Content='B' Width='32' Height='24' /></StackPanel></UserControl>", "StackPanel siblings"),
            ("<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'><Canvas><Button Content='A' Width='40' Height='24' Canvas.Left='40' Canvas.Top='48' /><Canvas Width='100' Height='100' Canvas.Left='200'><Button Content='B' Width='32' Height='24' /></Canvas></Canvas></UserControl>", "Mixed parents"),
            (source.Replace("Canvas.Left='112'", "Canvas.Left='{Binding ProtectedPosition}'"), "Protected sibling position")
        })
        {
            await Load(text);
            var original = doc.Text;
            Check(doc.SelectionService!.SelectionCount == 2, label + " fixture has two selected controls");
            Check(Handle("SplitGroupBorderDrag") == null && Handle("SplitGroupMoveHandle") == null, label + " has no group editing overlay");
            overlay.Focus();
            Key(Avalonia.Input.Key.Right);
            Check(doc.Text == original && !editor.Document.UndoStack.CanUndo, label + " cannot move only the primary through the keyboard");
        }

        Console.WriteLine("TOTAL GROUP INTERACTION FAILURES: " + failures);
        if (failures > 0) throw new Exception("Group interaction checks failed: " + failures);
    }
}
