using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.Designer;

internal static class SplitClipboardChecks
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
        var overlay = view.FindControl<Canvas>("SplitResizeOverlay")!;
        const string source = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'>\r\n  <!-- keep -->\r\n  <Canvas>\r\n    <Button Content='A' Width='40' Height='24' Canvas.Left='40' Canvas.Top='48' />\r\n    <Button Content='B' Width='32' Height='24' Canvas.Left='112' Canvas.Top='80' />\r\n  </Canvas>\r\n</UserControl>";

        async Task LoadAndSelect()
        {
            editor.Text = source;
            await Task.Delay(750);
            var items = doc.DesignSurface.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Content?.ToString() is "A" or "B")
                .Select(button => doc.DesignContext!.Services.View.GetModel(button)!)
                .ToArray();
            doc.SelectionService!.SetSelectedComponents(items, SelectionTypes.Replace);
            overlay.Focus();
            editor.Document.UndoStack.ClearAll();
        }
        void Key(Key key, KeyModifiers modifiers = KeyModifiers.None)
        {
            var focused = TopLevel.GetTopLevel(view)!.FocusManager!.GetFocusedElement() as InputElement;
            focused?.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
                KeyModifiers = modifiers
            });
        }

        await LoadAndSelect();
        Key(Avalonia.Input.Key.D, KeyModifiers.Control);
        await Task.Delay(750);
        var duplicates = doc.DesignSurface.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Content?.ToString() is "A" or "B").ToArray();
        Check(duplicates.Length == 4 && doc.Text != source,
            "Ctrl+D duplicates every selected control in XAML");
        Check(doc.Text.Contains("Canvas.Left='48'", StringComparison.Ordinal)
            && doc.Text.Contains("Canvas.Top='56'", StringComparison.Ordinal)
            && doc.Text.Contains("Canvas.Left='120'", StringComparison.Ordinal)
            && doc.Text.Contains("Canvas.Top='88'", StringComparison.Ordinal),
            "Duplicate offsets Canvas positions by exactly 8px");
        Check(doc.SelectionService?.SelectionCount == 2,
            "Duplicated controls become the active selection");
        var duplicatedSource = doc.Text;
        doc.UndoCommand.Execute(null);
        Check(doc.Text == source && !editor.Document.UndoStack.CanUndo,
            "Multi-control duplicate uses one Undo step");
        await Task.Delay(750);
        doc.RedoCommand.Execute(null);
        Check(doc.Text == duplicatedSource, "Duplicate supports Redo");

        await LoadAndSelect();
        Key(Avalonia.Input.Key.Delete);
        await Task.Delay(750);
        Check(!doc.DesignSurface.GetVisualDescendants().OfType<Button>()
                .Any(button => button.Content?.ToString() is "A" or "B")
            && !doc.Text.Contains("<Button", StringComparison.Ordinal),
            "Delete removes every selected control from XAML");
        Check(doc.SelectionService?.SelectedItems.All(item => item.Component is not Button) == true,
            "Delete leaves no stale removed controls selected");
        doc.UndoCommand.Execute(null);
        Check(doc.Text == source && !editor.Document.UndoStack.CanUndo,
            "Multi-control delete uses one Undo step");

        await LoadAndSelect();
        Key(Avalonia.Input.Key.C, KeyModifiers.Control);
        await Task.Delay(200);
        var clipboard = TopLevel.GetTopLevel(view)!.Clipboard!;
        var copied = await clipboard.GetTextAsync() ?? "";
        Check(copied.Contains("<Button Content='A'", StringComparison.Ordinal)
            && copied.Contains("<Button Content='B'", StringComparison.Ordinal),
            "Ctrl+C copies the exact selected XAML fragments");
        Check(doc.Text == source && !editor.Document.UndoStack.CanUndo,
            "Copy does not modify source or history");

        Key(Avalonia.Input.Key.V, KeyModifiers.Control);
        await Task.Delay(750);
        var pastedSource = doc.Text;
        Check(doc.DesignSurface.GetVisualDescendants().OfType<Button>()
                .Count(button => button.Content?.ToString() is "A" or "B") == 4
            && pastedSource != source,
            "Ctrl+V pastes every copied control into the same parent");
        Check(doc.SelectionService?.SelectionCount == 2,
            "Pasted controls become the active selection");
        doc.UndoCommand.Execute(null);
        Check(doc.Text == source && !editor.Document.UndoStack.CanUndo,
            "Multi-control paste uses one Undo step");

        await LoadAndSelect();
        Key(Avalonia.Input.Key.X, KeyModifiers.Control);
        await Task.Delay(750);
        copied = await clipboard.GetTextAsync() ?? "";
        Check(!doc.Text.Contains("<Button", StringComparison.Ordinal)
            && copied.Contains("<Button Content='A'", StringComparison.Ordinal)
            && copied.Contains("<Button Content='B'", StringComparison.Ordinal),
            "Ctrl+X copies exact XAML and removes all selected controls");
        Key(Avalonia.Input.Key.V, KeyModifiers.Control);
        await Task.Delay(750);
        Check(doc.DesignSurface.GetVisualDescendants().OfType<Button>()
                .Count(button => button.Content?.ToString() is "A" or "B") == 2
            && doc.SelectionService?.SelectionCount == 2,
            "Paste after cut restores controls into the remembered parent and selects them");
        doc.UndoCommand.Execute(null);
        await Task.Delay(750);
        doc.UndoCommand.Execute(null);
        Check(doc.Text == source && !editor.Document.UndoStack.CanUndo,
            "Cut and following paste each use one Undo step");

        const string gridSource = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'><Grid><Button Content='Grid' Width='40' Height='24' HorizontalAlignment='Left' VerticalAlignment='Top' Margin='1,2,3,4' /></Grid></UserControl>";
        editor.Text = gridSource;
        await Task.Delay(750);
        var gridButton = doc.DesignSurface.GetVisualDescendants().OfType<Button>()
            .Single(button => Equals(button.Content, "Grid"));
        doc.SelectionService!.SetSelectedComponents(new[] { doc.DesignContext!.Services.View.GetModel(gridButton)! }, SelectionTypes.Replace);
        overlay.Focus();
        Key(Avalonia.Input.Key.D, KeyModifiers.Control);
        await Task.Delay(750);
        var gridButtons = doc.DesignSurface.GetVisualDescendants().OfType<Button>()
            .Where(button => Equals(button.Content, "Grid")).OrderBy(button => button.Bounds.X).ToArray();
        Check(gridButtons.Length == 2
            && Math.Abs(gridButtons[1].Bounds.X - gridButtons[0].Bounds.X - 8) < .1
            && Math.Abs(gridButtons[1].Bounds.Y - gridButtons[0].Bounds.Y - 8) < .1,
            "Grid duplicate applies an 8px visual offset through Margin");

        const string protectedSource = "<UserControl xmlns='https://github.com/avaloniaui' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Width='400' Height='300'><UserControl.Resources><SolidColorBrush x:Key='Accent'>Red</SolidColorBrush></UserControl.Resources><Canvas><Button Content='Bound' Background='{DynamicResource Accent}' Width='{Binding ButtonWidth}' /></Canvas></UserControl>";
        editor.Text = protectedSource;
        await Task.Delay(750);
        var bound = doc.DesignSurface.GetVisualDescendants().OfType<Button>()
            .Single(button => Equals(button.Content, "Bound"));
        doc.SelectionService!.SetSelectedComponents(new[] { doc.DesignContext!.Services.View.GetModel(bound)! }, SelectionTypes.Replace);
        overlay.Focus();
        Key(Avalonia.Input.Key.D, KeyModifiers.Control);
        await Task.Delay(750);
        Check(doc.Text.Split("{DynamicResource Accent}").Length == 3
            && doc.Text.Split("{Binding ButtonWidth}").Length == 3
            && doc.Text.Contains("xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'", StringComparison.Ordinal),
            "Duplicate preserves bindings, resources, namespaces, and original root formatting");

        const string mixedSource = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'><Canvas><Button Content='A' /><Canvas><Button Content='Nested' /></Canvas></Canvas></UserControl>";
        editor.Text = mixedSource;
        await Task.Delay(750);
        var mixed = doc.DesignSurface.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Content?.ToString() is "A" or "Nested")
            .Select(button => doc.DesignContext!.Services.View.GetModel(button)!).ToArray();
        doc.SelectionService!.SetSelectedComponents(mixed, SelectionTypes.Replace);
        overlay.Focus();
        Key(Avalonia.Input.Key.D, KeyModifiers.Control);
        Key(Avalonia.Input.Key.Delete);
        Check(doc.Text == mixedSource, "Mixed-parent clipboard mutations are rejected atomically");

        doc.SelectionService.SetSelectedComponents(new[] { mixed.Single(item => Equals(((Button)item.Component).Content, "A")) },
            SelectionTypes.Replace);
        await clipboard.SetTextAsync("not valid XAML <");
        var beforeInvalidPaste = doc.Text;
        Key(Avalonia.Input.Key.V, KeyModifiers.Control);
        await Task.Delay(200);
        Check(doc.Text == beforeInvalidPaste && !doc.HasPreviewError,
            "Malformed external clipboard text is rejected without changing XAML");

        Console.WriteLine($"TOTAL CLIPBOARD FAILURES: {failures}");
        if (failures > 0) throw new Exception($"Clipboard checks failed: {failures}");
    }
}
