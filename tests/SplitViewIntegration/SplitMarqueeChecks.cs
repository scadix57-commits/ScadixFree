using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.Designer;

internal static class SplitMarqueeChecks
{
    public static async Task Run(Document doc, DocumentView view)
    {
        var failures = 0;
        void Check(bool ok, string message)
        {
            Console.WriteLine((ok ? "PASS: " : "FAIL: ") + message);
            if (!ok) failures++;
        }

        var editor = view.GetVisualDescendants().OfType<XamlEditorView>().Single().Editor!;
        var selectionOverlay = view.FindControl<Border>("PreviewSelectionOverlay")!;
        var resizeOverlay = view.FindControl<Canvas>("SplitResizeOverlay")!;
        const string source = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300' Background='Transparent'><Canvas Width='360' Height='260' HorizontalAlignment='Left' VerticalAlignment='Top' Background='Transparent'><Button Content='A' Width='40' Height='24' Canvas.Left='40' Canvas.Top='48' /><Button Content='B' Width='32' Height='24' Canvas.Left='112' Canvas.Top='80' /><Button Content='Outside' Width='40' Height='24' Canvas.Left='260' Canvas.Top='180' /></Canvas></UserControl>";

        Button Button(string content) => doc.DesignSurface.GetVisualDescendants().OfType<Button>()
            .Single(button => Equals(button.Content, content));
        void PressMove(Point start, Point end, Pointer pointer, KeyModifiers modifiers = KeyModifiers.None)
        {
            selectionOverlay.RaiseEvent(new PointerPressedEventArgs(selectionOverlay, pointer, selectionOverlay, start, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), modifiers, 1));
            selectionOverlay.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, selectionOverlay, pointer, selectionOverlay, end, 1,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), modifiers));
        }
        void Release(Point end, Pointer pointer, KeyModifiers modifiers = KeyModifiers.None)
            => selectionOverlay.RaiseEvent(new PointerReleasedEventArgs(selectionOverlay, pointer, selectionOverlay, end, 2,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), modifiers, MouseButton.Left));
        string[] SelectedButtons() => doc.SelectionService!.SelectedItems
            .Select(item => (item.Component as Button)?.Content?.ToString())
            .Where(content => content != null)
            .Cast<string>()
            .ToArray();

        editor.Text = source;
        await Task.Delay(750);

        var canvas = doc.DesignSurface.GetVisualDescendants().OfType<Canvas>().First(c => c.Width == 360);
        var start = canvas.TranslatePoint(new Point(8, 8), selectionOverlay)!.Value;
        var end = canvas.TranslatePoint(new Point(180, 130), selectionOverlay)!.Value;
        using var pointer = new Pointer(501, PointerType.Mouse, true);
        PressMove(start, end, pointer);

        Check(resizeOverlay.Children.OfType<Border>().Any(border => border.Name == "SplitMarqueeSelection"),
            "Dragging empty preview space shows a marquee rectangle");

        Release(end, pointer);

        var selected = SelectedButtons();
        Check(selected.Length == 2 && selected.Contains("A") && selected.Contains("B"),
            "Marquee selects intersecting sibling controls and excludes controls outside it (selected: "
            + string.Join(", ", selected) + ")");
        Check(!resizeOverlay.Children.OfType<Border>().Any(border => border.Name == "SplitMarqueeSelection"),
            "Releasing the pointer removes the marquee rectangle");

        using var escapePointer = new Pointer(502, PointerType.Mouse, true);
        PressMove(start, end, escapePointer);
        var focused = TopLevel.GetTopLevel(view)!.FocusManager!.GetFocusedElement() as InputElement;
        focused?.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        var marqueeVisibleAfterEscape = resizeOverlay.Children.OfType<Border>()
            .Any(border => border.Name == "SplitMarqueeSelection");
        Check(!marqueeVisibleAfterEscape && doc.SelectionService.SelectionCount == 2,
            $"Escape cancels marquee without changing the current selection (visible: {marqueeVisibleAfterEscape}, selected: {doc.SelectionService.SelectionCount})");

        var emptyStart = canvas.TranslatePoint(new Point(210, 120), selectionOverlay)!.Value;
        var emptyEnd = canvas.TranslatePoint(new Point(230, 145), selectionOverlay)!.Value;
        using var emptyPointer = new Pointer(503, PointerType.Mouse, true);
        PressMove(emptyStart, emptyEnd, emptyPointer);
        Release(emptyEnd, emptyPointer);
        Check(doc.SelectionService.SelectionCount == 0, "Empty marquee clears selection in replace mode");

        doc.SelectionService.SetSelectedComponents(new[] { doc.DesignContext!.Services.View.GetModel(Button("A"))! });
        using var togglePointer = new Pointer(504, PointerType.Mouse, true);
        PressMove(start, end, togglePointer, KeyModifiers.Control);
        Release(end, togglePointer, KeyModifiers.Control);
        selected = SelectedButtons();
        Check(selected.Length == 1 && selected[0] == "B", "Ctrl+marquee toggles siblings in the current parent");

        const string nestedSource = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'><Canvas Width='360' Height='260' HorizontalAlignment='Left' VerticalAlignment='Top' Background='Transparent'><Button Content='A' Width='40' Height='24' Canvas.Left='40' Canvas.Top='48' /><Button Content='B' Width='32' Height='24' Canvas.Left='112' Canvas.Top='80' /><Canvas Width='80' Height='60' Canvas.Left='250' Canvas.Top='170'><Button Content='Nested' Width='40' Height='24' /></Canvas></Canvas></UserControl>";
        editor.Text = nestedSource;
        await Task.Delay(750);
        canvas = doc.DesignSurface.GetVisualDescendants().OfType<Canvas>().First(c => c.Width == 360);
        doc.SelectionService!.SetSelectedComponents(new[] { doc.DesignContext!.Services.View.GetModel(Button("Nested"))! });
        var nestedStart = canvas.TranslatePoint(new Point(8, 8), selectionOverlay)!.Value;
        var nestedEnd = canvas.TranslatePoint(new Point(180, 130), selectionOverlay)!.Value;
        using var mixedPointer = new Pointer(505, PointerType.Mouse, true);
        PressMove(nestedStart, nestedEnd, mixedPointer, KeyModifiers.Control);
        Release(nestedEnd, mixedPointer, KeyModifiers.Control);
        selected = SelectedButtons();
        Check(selected.Length == 2 && selected.Contains("A") && selected.Contains("B"),
            "Ctrl+marquee replaces a selection from a different parent");

        var buttonPoint = Button("A").TranslatePoint(new Point(4, 4), selectionOverlay)!.Value;
        using var controlPointer = new Pointer(506, PointerType.Mouse, true);
        PressMove(buttonPoint, buttonPoint + new Vector(40, 40), controlPointer);
        Check(!resizeOverlay.Children.OfType<Border>().Any(border => border.Name == "SplitMarqueeSelection"),
            "Dragging from a control does not start marquee selection");
        Release(buttonPoint + new Vector(40, 40), controlPointer);

        using var modePointer = new Pointer(507, PointerType.Mouse, true);
        PressMove(nestedStart, nestedEnd, modePointer);
        Check(resizeOverlay.Children.OfType<Border>().Any(border => border.Name == "SplitMarqueeSelection"),
            "Marquee is active before leaving Split mode");
        doc.Mode = DocumentMode.Xaml;
        Check(!resizeOverlay.Children.OfType<Border>().Any(border => border.Name == "SplitMarqueeSelection"),
            "Leaving Split mode cancels an active marquee");
        doc.Mode = DocumentMode.Split;
        await Task.Delay(100);

        canvas = doc.DesignSurface.GetVisualDescendants().OfType<Canvas>().First(c => c.Width == 360);
        nestedStart = canvas.TranslatePoint(new Point(8, 8), selectionOverlay)!.Value;
        nestedEnd = canvas.TranslatePoint(new Point(180, 130), selectionOverlay)!.Value;
        using var sourcePointer = new Pointer(508, PointerType.Mouse, true);
        PressMove(nestedStart, nestedEnd, sourcePointer);
        editor.Text = nestedSource.Replace("Content='A'", "Content='Updated'");
        Check(!resizeOverlay.Children.OfType<Border>().Any(border => border.Name == "SplitMarqueeSelection"),
            "Typing cancels an active marquee before preview reload");

        Console.WriteLine($"TOTAL MARQUEE FAILURES: {failures}");
        if (failures > 0) throw new Exception($"Marquee checks failed: {failures}");
    }
}
