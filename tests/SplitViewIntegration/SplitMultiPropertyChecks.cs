using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.PropertyGrid;
using Scadix.Designer;

internal static class SplitMultiPropertyChecks
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
        Window? propertiesWindow = null;
        try
        {
            MainWindowViewModel.Instance.CurrentDocument = doc;
            var properties = new PropertiesToolView();
            propertiesWindow = new Window { Content = properties, Width = 380, Height = 600 };
            propertiesWindow.Show();
            await Task.Delay(100);
            var propertyGrid = properties.FindControl<PropertyGridView>("uxPropertyGridView")!;

            const string source = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'>\r\n  <!-- keep -->\r\n  <Canvas>\r\n    <Button Content='A' Width='40' Height='24' />\r\n    <Button Content='B' Width=\"60\" Height='24' />\r\n  </Canvas>\r\n</UserControl>";
            async Task Load(string text)
            {
                editor.Text = text;
                await Task.Delay(750);
                var buttons = doc.DesignSurface.GetVisualDescendants().OfType<Button>()
                    .Where(button => button.Content?.ToString() is "A" or "B")
                    .Select(button => doc.DesignContext!.Services.View.GetModel(button)!)
                    .ToArray();
                doc.SelectionService!.SetSelectedComponents(buttons, SelectionTypes.Replace);
                await Task.Delay(100);
                editor.Document.UndoStack.ClearAll();
            }
            TextBox Field(string name) => (TextBox)propertyGrid.PropertyGrid.NodeFromDescriptor.Values
                .Single(node => node.IsVisible && node.Name == name).Editor;
            void Commit(TextBox field, string value)
            {
                field.Text = value;
                field.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            }

            await Load(source);
            var width = Field("Width");
            Check(width.Text == "" && Equals(width.Watermark, "Multiple values") && !width.IsReadOnly,
                "Different values show an editable Multiple values state");
            Check(Field("Height").Text == "24", "Matching values display their shared value");

            Commit(width, "80");
            Check(doc.Text == source.Replace("Width='40'", "Width='80'").Replace("Width=\"60\"", "Width=\"80\""),
                "Multi-edit updates every selected attribute and preserves quotes and formatting");
            await Task.Delay(750);
            Check(doc.SelectionService!.SelectionCount == 2
                && doc.DesignSurface.GetVisualDescendants().OfType<Button>()
                    .Where(button => button.Content?.ToString() is "A" or "B").All(button => button.Width == 80),
                "Multi-edit refreshes both controls and retains selection");
            doc.UndoCommand.Execute(null);
            Check(doc.Text == source && !editor.Document.UndoStack.CanUndo, "Multi-edit is one Undo step");
            await Task.Delay(750);
            doc.RedoCommand.Execute(null);
            Check(doc.Text.Contains("Width='80'") && doc.Text.Contains("Width=\"80\""), "Multi-edit supports Redo");

            await Load(source);
            var margin = Field("Margin");
            Commit(margin, "1,2,3,4");
            Check(doc.Text == source
                    .Replace("<Button Content='A'", "<Button Margin=\"1,2,3,4\" Content='A'")
                    .Replace("<Button Content='B'", "<Button Margin=\"1,2,3,4\" Content='B'"),
                "Multi-edit inserts a missing attribute on every item without reformatting XAML");
            doc.UndoCommand.Execute(null);
            Check(doc.Text == source && !editor.Document.UndoStack.CanUndo,
                "Inserted multi-edit attributes use one Undo step");

            await Load(source);
            var invalidWidth = Field("Width");
            Commit(invalidWidth, "-1");
            Check(doc.Text == source && !editor.Document.UndoStack.CanUndo,
                "Invalid multi-edit value leaves every item unchanged");

            var onlyA = doc.SelectionService!.SelectedItems
                .Single(item => Equals(((Button)item.Component).Content, "A"));
            doc.SelectionService.SetSelectedComponents(new[] { onlyA }, SelectionTypes.Replace);
            await Task.Delay(100);
            var singleWidth = Field("Width");
            Commit(singleWidth, "-1");
            Commit(singleWidth, "Auto");
            await Task.Delay(750);
            Check(!doc.HasPreviewError
                && doc.SelectionService?.PrimarySelection?.Component is Button autoButton
                && double.IsNaN(autoButton.Width),
                "A valid single edit commits after an invalid value");

            const string protectedSource = "<UserControl xmlns='https://github.com/avaloniaui' Width='400' Height='300'><Canvas><Button Content='A' Width='{Binding Value}' Height='24' /><Button Content='B' Width='60' Height='24' /></Canvas></UserControl>";
            await Load(protectedSource);
            Check(Field("Width").IsReadOnly, "A binding on one item makes the shared property read-only");

            await Load(source);
            var staleWidth = Field("Width");
            var first = doc.SelectionService!.SelectedItems.First();
            doc.SelectionService.SetSelectedComponents(new[] { first }, SelectionTypes.Replace);
            Commit(staleWidth, "96");
            Check(doc.Text == source, "Editor from a previous multi-selection cannot modify source");
        }
        finally
        {
            propertiesWindow?.Close();
        }

        Console.WriteLine($"TOTAL MULTI-PROPERTY FAILURES: {failures}");
        if (failures > 0) throw new Exception($"Multi-property checks failed: {failures}");
    }
}
