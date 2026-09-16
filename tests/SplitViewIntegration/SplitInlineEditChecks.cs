using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.Designer;

internal static class SplitInlineEditChecks
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
        var startInlineEdit = typeof(DocumentView).GetMethod("StartInlineEdit", BindingFlags.Instance | BindingFlags.NonPublic)!;
        const string source = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'>\r\n  <!-- keep -->\r\n  <Canvas>\r\n    <Button Content='Original' Width='120' Height='32' Canvas.Left='40' Canvas.Top='48' />\r\n  </Canvas>\r\n</UserControl>";

        async Task<(Button Control, DesignItem Item)> Load(string text = source)
        {
            editor.Text = text;
            await Task.Delay(750);
            var expectedContent = text.Contains("{Binding Name}", StringComparison.Ordinal) ? null : "Original";
            var button = doc.DesignSurface.GetVisualDescendants().OfType<Button>()
                .First(candidate => doc.DesignContext!.Services.View.GetModel(candidate) != null
                    && (expectedContent == null || candidate.Content?.ToString() == expectedContent));
            var item = doc.DesignContext!.Services.View.GetModel(button)!;
            doc.SelectionService!.SetSelectedComponents(new[] { item }, SelectionTypes.Replace);
            editor.Document.UndoStack.ClearAll();
            return (button, item);
        }

        static void SendKey(TextBox textBox, Key key) => textBox.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key
        });

        var (button, item) = await Load();
        var previewBounds = doc.DesignSurface.Bounds;
        var content = item.Properties.GetProperty("Content");
        startInlineEdit.Invoke(view, new object[] { item, content, button });
        var inlineOverlay = view.FindControl<Canvas>("InlineEditOverlay")!;
        var inlineEditor = inlineOverlay.Children.OfType<TextBox>().Single();
        Check(inlineOverlay.IsVisible && inlineEditor.Text == "Original", "Inline editor opens with the selected control's content");
        Check(inlineEditor.Bounds.Width <= button.Bounds.Width + 1, "Inline editor is positioned over the selected control");
        inlineEditor.Text = "Edited & <text>";
        SendKey(inlineEditor, Key.Enter);
        await Task.Delay(750);
        Check(doc.Text.Contains("Content='Edited &amp; &lt;text&gt;'", StringComparison.Ordinal), "Enter commits escaped content to XAML");
        Check(doc.SelectionService?.PrimarySelection?.Component is Button edited && edited.Content?.ToString() == "Edited & <text>", "Committed inline edit refreshes preview and retains selection");
        var editedSource = doc.Text;
        doc.UndoCommand.Execute(null);
        Check(doc.Text == source && !editor.Document.UndoStack.CanUndo, "Inline edit creates one Undo entry");
        await Task.Delay(750);
        doc.RedoCommand.Execute(null);
        Check(doc.Text == editedSource, "Inline edit supports Redo");

        (button, item) = await Load();
        content = item.Properties.GetProperty("Content");
        startInlineEdit.Invoke(view, new object[] { item, content, button });
        inlineEditor = inlineOverlay.Children.OfType<TextBox>().Single();
        inlineEditor.Text = "Cancelled";
        SendKey(inlineEditor, Key.Escape);
        Check(doc.Text == source && !inlineOverlay.IsVisible && !editor.Document.UndoStack.CanUndo, "Escape cancels without changing XAML or history");

        const string bindingSource = "<UserControl xmlns='https://github.com/avaloniaui'><Button Content='{Binding Name}' /></UserControl>";
        (button, item) = await Load(bindingSource);
        var isBinding = typeof(DocumentView).GetMethod("IsProtectedSourceExpression", BindingFlags.Instance | BindingFlags.NonPublic)!;
        content = item.Properties.GetProperty("Content");
        Check((bool)isBinding.Invoke(view, new object[] { item, "Content" })!, "Binding content is recognized as a protected expression");
        Check(doc.Text == bindingSource, "Inspecting protected content preserves the binding source");

        var quickPanel = view.FindControl<Border>("QuickActionsPanel")!;
        Check(quickPanel.IsVisible, "Quick Actions appears for a Split preview selection");
        Check(doc.DesignSurface.Bounds == previewBounds, "Quick Actions does not resize the design surface");

        (button, item) = await Load();
        var setProperty = typeof(DocumentView).GetMethod("SetPropertyValue", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var width = item.Properties.GetProperty("Width");
        setProperty.Invoke(view, new object[] { width, new[] { item }, 144d });
        await Task.Delay(750);
        Check(doc.Text.Contains("Width='144'", StringComparison.Ordinal), "Quick Actions writes property changes to XAML");
        Check(doc.SelectionService?.PrimarySelection?.Component is Button resized && resized.Width == 144, "Quick Actions refreshes preview and retains selection");
        doc.UndoCommand.Execute(null);
        Check(doc.Text == source && !editor.Document.UndoStack.CanUndo, "Quick Actions property change creates one Undo entry");

        Console.WriteLine("TOTAL INLINE EDIT FAILURES: " + failures);
        if (failures > 0) throw new Exception("Inline edit checks failed: " + failures);
    }
}
