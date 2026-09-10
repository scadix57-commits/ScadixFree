using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.Designer;
using System.Globalization;

internal static class SplitMoveChecks
{
    public static async Task Run(Document doc, DocumentView view)
    {
        int failures = 0;
        void Check(bool ok, string message)
        {
            Console.WriteLine((ok ? "PASS: " : "FAIL: ") + message);
            if (!ok) failures++;
        }
        var editor = view.GetVisualDescendants().OfType<XamlEditorView>().Single().Editor!;
        var overlay = view.FindControl<Canvas>("SplitResizeOverlay")!;
        doc.DesignContext!.Services.GetService<Scadix.AxamlDesign.ISplitResizeOverlayService>()!.SnapEnabled = false;
        const string source = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\">\r\n<!-- keep -->\r\n<Canvas><Button Content='Move' Width='120' Height='60' Canvas.Left='10.5' Canvas.Top='20.5' /></Canvas></UserControl>";
        async Task Load(string text)
        {
            editor.Text = text;
            await Task.Delay(750);
            editor.Select(text.IndexOf("<Button", StringComparison.Ordinal) + 2, 0);
            await Task.Delay(100);
        }
        Control Handle(string name) => overlay.Children.OfType<Control>().Single(c => c.Name == name);
        void Drag(string name, bool escape = false, bool inspect = false)
        {
            var handle = Handle(name);
            var button = (Button)doc.SelectionService!.PrimarySelection!.Component;
            var root = (Visual)TopLevel.GetTopLevel(handle)!;
            var start = handle.TranslatePoint(new Point(4, 4), root)!.Value;
            var delta = button.TranslatePoint(new Point(20, 10), root)!.Value - button.TranslatePoint(default, root)!.Value;
            using var pointer = new Pointer(19, PointerType.Mouse, true);
            var before = doc.Text;
            handle.RaiseEvent(new PointerPressedEventArgs(handle, pointer, root, start, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
            handle.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, handle, pointer, root, start + delta, 1,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
            Check(doc.Text == before, "Move defers source edit until release");
            if (inspect)
            {
                var expected = button.TranslatePoint(new Point(20, 10), overlay)!.Value;
                var border = Handle("SplitBorderDrag");
                Check(Math.Abs(Canvas.GetLeft(border) - expected.X) < .1 && Math.Abs(Canvas.GetTop(border) - expected.Y) < .1,
                    "Move ghost follows transformed pointer coordinates");
            }
            if (escape)
            {
                var focused = TopLevel.GetTopLevel(handle)!.FocusManager!.GetFocusedElement() as InputElement;
                Check(ReferenceEquals(focused, handle), "Drag target receives keyboard focus for Escape");
                focused?.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            }
            handle.RaiseEvent(new PointerReleasedEventArgs(handle, pointer, root, start + delta, 2,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        }
        await Load(source);
        var move = Handle("SplitMoveHandle");
        Check(ReferenceEquals(view.InputHitTest(move.TranslatePoint(new Point(6, 6), view)!.Value), move), "Actual hit testing reaches move handle");
        var corner = Handle("SplitResizeBottomRight");
        Check(ReferenceEquals(view.InputHitTest(corner.TranslatePoint(new Point(2, 2), view)!.Value), corner), "Resize handle inner area remains clickable");
        Drag("SplitMoveHandle");
        var moved = source.Replace("'10.5'", "'30.5'").Replace("'20.5'", "'30.5'");
        Check(doc.Text == moved, "Canvas move preserves exact surrounding XAML");
        await Task.Delay(750);
        doc.UndoCommand.Execute(null);
        Check(doc.Text == source, "One Undo restores both coordinates");
        await Task.Delay(750);
        doc.RedoCommand.Execute(null);
        Check(doc.Text == moved, "Redo restores move");
        await Load(source);
        Drag("SplitMoveHandle", escape: true);
        Check(doc.Text == source, "Escape cancels move handle drag");
        await Load(source);
        Drag("SplitBorderDrag", escape: true);
        Check(doc.Text == source, "Escape cancels body drag using actual keyboard focus");
        await Load(source);
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Drag("SplitMoveHandle");
            Check(doc.Text == moved, "Decimal coordinates are independent of current culture (actual: " + doc.Text + ")");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        await Load(source);
        ((Button)doc.SelectionService!.PrimarySelection!.Component).RenderTransform = new Avalonia.Media.ScaleTransform(1.5, 1.5);
        await Task.Delay(100);
        Drag("SplitMoveHandle", inspect: true);
        Check(doc.Text == source.Replace("'10.5'", "'40.5'").Replace("'20.5'", "'35.5'"), "Scaled control move commits displacement in parent coordinates");
        var gridSource = source.Replace("<Canvas>", "<Grid>").Replace("</Canvas>", "</Grid>")
            .Replace("Canvas.Left='10.5' Canvas.Top='20.5'", "Margin='10,20,0,0'");
        await Load(gridSource);
        var beforeButton = (Button)doc.SelectionService!.PrimarySelection!.Component;
        var beforePos = beforeButton.Bounds.Position;
        Drag("SplitMoveHandle");
        Check(doc.Text == gridSource.Replace("'10,20,0,0'", "'30,40,0,0'"), "Grid move updates Margin source");
        await Task.Delay(750);
        var afterButton = (Button)doc.SelectionService!.PrimarySelection!.Component;
        Check(Math.Abs(afterButton.Bounds.X - beforePos.X - 20) < .1 && Math.Abs(afterButton.Bounds.Y - beforePos.Y - 10) < .1,
            $"Grid actual displacement matches drag (actual {afterButton.Bounds.Position - beforePos})");
        foreach (var panel in new[] { "Canvas", "Grid" })
        {
            var fixture = panel == "Canvas" ? source : gridSource.Replace("'10,20,0,0'", "'10,20,7,9'");
            var expected = panel == "Canvas" ? moved : fixture.Replace("'10,20,7,9'", "'30,40,7,9'");
            await Load(fixture);
            var border = Handle("SplitBorderDrag");
            Check(ReferenceEquals(view.InputHitTest(border.TranslatePoint(new Point(15, 15), view)!.Value), border), panel + " body drag is hit-testable");
            Drag("SplitBorderDrag");
            Check(doc.Text == expected, panel + " body drag preserves formatting and untouched coordinates");
            await Task.Delay(750);
            doc.UndoCommand.Execute(null);
            Check(doc.Text == fixture, panel + " body drag Undo is one step");
            await Task.Delay(750);
            doc.RedoCommand.Execute(null);
            Check(doc.Text == expected, panel + " body drag Redo works");
            foreach (var name in new[] { "SplitMoveHandle", "SplitBorderDrag" })
            {
                await Load(fixture);
                Drag(name, escape: true);
                Check(doc.Text == fixture, panel + " Escape cancels " + name);
            }
        }
        foreach (var panel in new[] { "StackPanel", "WrapPanel" })
        {
            var fixture = source.Replace("<Canvas>", "<Canvas><" + panel + ">").Replace("</Canvas>", "</" + panel + "></Canvas>");
            await Load(fixture);
            Check(!overlay.Children.OfType<Control>().Any(c => c.Name is "SplitMoveHandle" or "SplitBorderDrag"), panel + " direct parent prevents move despite Canvas ancestor");
            Check(overlay.Children.OfType<Control>().Count(c => c.Name?.StartsWith("SplitResize") == true) == 8, panel + " retains eight resize handles");
        }
        var nested = source.Replace("<Canvas>", "<Grid Margin='3'><Canvas>").Replace("</Canvas>", "</Canvas></Grid>");
        await Load(nested);
        Drag("SplitMoveHandle");
        Check(doc.Text == nested.Replace("'10.5'", "'30.5'").Replace("'20.5'", "'30.5'"), "Nested Canvas move edits only child coordinates, leaves outer Grid intact");
        foreach (var expression in new[] { "{Binding Position}", "{DynamicResource Position}" })
        {
            var fixture = source.Replace("'10.5'", "'" + expression + "'");
            await Load(fixture);
            if (overlay.Children.OfType<Control>().Any(c => c.Name == "SplitMoveHandle")) Drag("SplitMoveHandle");
            Check(doc.Text.Contains("Canvas.Left='" + expression + "'"), "Canvas preserves " + expression);
            fixture = gridSource.Replace("'10,20,0,0'", "'" + expression + "'");
            await Load(fixture);
            Check(!overlay.Children.OfType<Control>().Any(c => c.Name is "SplitMoveHandle" or "SplitBorderDrag"), "Grid protects Margin " + expression);
            Check(doc.Text == fixture, "Grid protected source remains exact");
        }
        var anchored = source.Replace("Canvas.Left='10.5' Canvas.Top='20.5'", "Canvas.Right='10' Canvas.Bottom='20'");
        await Load(anchored);
        var anchoredButton = (Button)doc.SelectionService!.PrimarySelection!.Component;
        var anchoredPosition = anchoredButton.Bounds.Position;
        Drag("SplitMoveHandle");
        await Task.Delay(750);
        anchoredButton = (Button)doc.SelectionService!.PrimarySelection!.Component;
        Check(anchoredButton.Bounds.Position == anchoredPosition + new Vector(20, 10), "Canvas opposite anchors do not jump when leading coordinates are inserted");
        var propertyElement = source.Replace(" Canvas.Left='10.5'", "").Replace(" /></Canvas>", "><Canvas.Left>10.5</Canvas.Left></Button></Canvas>");
        await Load(propertyElement);
        Drag("SplitMoveHandle");
        Check(!doc.Text.Contains("Canvas.Left="), "Attached property element is not duplicated by move");
        var styled = gridSource.Replace(" Margin='10,20,0,0'", "").Replace("<Grid>", "<Grid><Grid.Styles><Style Selector='Button'><Setter Property='Margin' Value='10,20,7,9'/></Style></Grid.Styles>");
        await Load(styled);
        Drag("SplitMoveHandle");
        Check(doc.Text.Contains("Margin=\"30,40,7,9\""), "Missing Grid Margin starts from effective styled value");
        foreach (var alignment in new[] { "HorizontalAlignment='Right'", "VerticalAlignment='Bottom'" })
        {
            await Load(gridSource.Replace("<Button", "<Button " + alignment));
            Check(overlay.Children.OfType<Control>().Any(c => c.Name == "SplitMoveHandle"), "Grid allows moving trailing alignment for " + alignment);
        }
        Console.WriteLine("TOTAL MOVE FAILURES: " + failures);
        if (failures > 0) throw new Exception("Move regression checks failed: " + failures);
    }
}
