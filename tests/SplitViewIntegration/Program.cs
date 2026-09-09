using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Scadix.Designer;
using Avalonia.Input;
using Scadix.AxamlDesigner;
using Scadix.AxamlDesigner.PropertyGrid;

internal static class Program
{
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
    static string Markup(string label) => $"<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"200\"><TextBlock Text=\"{label}\" /></UserControl>";
    [STAThread]
    static int Main()
    {
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        using var stop = new CancellationTokenSource();
        var failed = false;
        Dispatcher.UIThread.Post(async () =>
        {
            Window? window = null;
            Window? propertiesWindow = null;
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".axaml");
            try
            {
                Check(Enum.TryParse<DocumentMode>("Split", out var split), "Split mode exists");
                Scadix.AxamlDesigner.BasicMetadata.Register();
                File.WriteAllText(path, Markup("Initial"));
                var doc = new Document(path);
                var view = new DocumentView { DataContext = doc };
                window = new Window { Content = view, Width = 1100, Height = 650 };
                window.Show();
                await Task.Delay(200);
                doc.Mode = split;
                await Task.Delay(100);
                var editor = view.GetVisualDescendants().OfType<XamlEditorView>().Single();
                var splitter = view.GetVisualDescendants().OfType<GridSplitter>().Single();
                Check(editor.IsVisible && doc.DesignSurface.IsEffectivelyVisible && splitter.IsVisible, "Editor and preview visible side by side");
                var overlay = view.FindControl<Border>("PreviewSelectionOverlay");
                Check(overlay is { IsVisible: true } && doc.SelectionService != null, "Split accepts preview selection");
                var label = doc.DesignSurface.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Initial");
                void Click(Control target)
                {
                    var point = target.TranslatePoint(new Point(4, 4), overlay!)!.Value;
                    using var pointer = new Pointer(1, PointerType.Mouse, true);
                    overlay!.RaiseEvent(new PointerPressedEventArgs(overlay, pointer, overlay, point, 0,
                        new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
                }
                Click(label);
                Check(doc.SelectionService!.PrimarySelection?.Component == label, "Click selects nested preview control");
                Check(editor.Editor!.SelectedText == "<TextBlock Text=\"Initial\" />", "Preview click selects matching XAML tag");
                Check(doc.SelectionService.PrimarySelection!.CreateOutlineNode().IsSelected, "Outline follows preview selection");
                Check(!doc.DesignSurface.IsEffectivelyEnabled, "Preview selection does not enable drag or resize");
                var selectedSource = doc.Text;
                doc.Save();
                Check(doc.Text == selectedSource && File.ReadAllText(path) == selectedSource, "Selection preserves exact saved source");
                MainWindowViewModel.Instance.CurrentDocument = doc;
                var properties = new PropertiesToolView();
                propertiesWindow = new Window { Content = properties, Width = 380, Height = 600 };
                propertiesWindow.Show();
                await Task.Delay(100);
                var propertyGrid = properties.FindControl<PropertyGridView>("uxPropertyGridView")!;
                Check(propertyGrid.SelectedItems.Single().Component == label && propertyGrid.IsEffectivelyEnabled,
                    "Properties remains navigable while inspecting Split selection");
                TextBox PropertyEditor(string name) => (TextBox)propertyGrid.PropertyGrid.NodeFromDescriptor.Values.Single(n => n.IsVisible && n.Name == name).Editor;
                void Commit(TextBox field, string value)
                {
                    field.Text = value;
                    field.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                }
                var textField = propertyGrid.PropertyGrid.NodeFromDescriptor.Values.Single(n => n.IsVisible && n.Name == "Text").Editor;
                Check(textField is TextBox { IsReadOnly: false, IsEffectivelyEnabled: true }, "Simple property can be edited in Split");
                var original = doc.Text;
                Commit((TextBox)textField, "Edited & <text>");
                Check(doc.Text == original.Replace("Text=\"Initial\"", "Text=\"Edited &amp; &lt;text&gt;\""), "Property edit escapes XML and patches only its attribute");
                await Task.Delay(750);
                Check(doc.SelectionService?.PrimarySelection?.Component is TextBlock edited && edited.Text == "Edited & <text>", "Property edit refreshes preview and retains selection");
                doc.UndoCommand.Execute(null);
                Check(doc.Text == original, "One Undo restores the whole property edit");
                await Task.Delay(750);
                doc.RedoCommand.Execute(null);
                Check(doc.Text.Contains("Edited &amp; &lt;text&gt;"), "Redo reapplies property edit");
                await Task.Delay(750);
                editor.Editor.Document.UndoStack.Undo();
                await Task.Delay(750);
                label = doc.DesignSurface.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Initial");
                Check(!propertyGrid.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "NameTextBox").IsEffectivelyEnabled,
                    "Control rename is disabled in Split");
                var outline = new OutlineToolView { DataContext = MainWindowViewModel.Instance };
                var outlineControl = (Control)outline.Content!;
                Check(!outlineControl.IsEffectivelyEnabled, "Outline cannot mutate read-only Split preview");
                var before = doc.DesignContext;
                editor.Editor!.Text = Markup("First edit");
                Click(label);
                Check(doc.SelectionService == null && doc.DesignContext.Services.Selection.PrimarySelection == null,
                    "Typing clears selection and stale preview clicks are ignored");
                await Task.Delay(200);
                editor.Editor.Text = Markup("Latest edit");
                await Task.Delay(200);
                Check(ReferenceEquals(before, doc.DesignContext), "Preview waits until typing pauses");
                await Task.Delay(650);
                Check(!ReferenceEquals(before, doc.DesignContext) && doc.DesignSurface.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Latest edit"), "Preview renders latest code after debounce");
                var latest = doc.DesignSurface.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Latest edit");
                Click(latest);
                Check(doc.SelectionService!.PrimarySelection?.Component == latest && propertyGrid.SelectedItems.Single().Component == latest,
                    "Selection and Properties reconnect after preview reload");
                editor.Editor.Text = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"200\">\n  <Border Padding=\"16\">\n    <StackPanel>\n      <Button Content=\"Inspect > this\" />\n    </StackPanel>\n  </Border>\n</UserControl>";
                await Task.Delay(750);
                var button = doc.DesignSurface.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Inspect > this"));
                Click(button);
                Check(doc.SelectionService!.PrimarySelection?.Component == button && editor.Editor.SelectedText == "<Button Content=\"Inspect > this\" />",
                    "Nested templated control maps to multiline XAML with quoted angle bracket");
                var exactText = editor.Editor.Text;
                editor.Editor.Select(0, 0);
                Check(doc.SelectionService!.PrimarySelection?.Component is UserControl, "Root opening tag selects root preview control");
                var attributeOffset = exactText.IndexOf("Inspect > this", StringComparison.Ordinal) + 3;
                editor.Editor.CaretOffset = attributeOffset;
                Check(doc.SelectionService!.PrimarySelection?.Component == button, "Source caret selects nested preview control");
                Check(editor.Editor.CaretOffset == attributeOffset && editor.Editor.SelectionLength == 0,
                    "Source selection does not move caret or select text");
                Check(propertyGrid.SelectedItems.Single().Component == button && doc.SelectionService.PrimarySelection!.CreateOutlineNode().IsSelected,
                    "Properties and Outline follow source caret");
                Click(button);
                Check(editor.Editor.SelectedText == "<Button Content=\"Inspect > this\" />", "Clicking source-selected control still selects its XAML tag");
                editor.Editor.TextArea.Caret.Offset = exactText.IndexOf("Padding", StringComparison.Ordinal);
                editor.Editor.TextArea.ClearSelection();
                Check(doc.SelectionService.PrimarySelection?.Component is Border, "Source mouse click after preview selection follows the new caret");
                editor.Editor.Select(0, 0);
                editor.Editor.CaretOffset = exactText.IndexOf("</StackPanel>", StringComparison.Ordinal) + 4;
                Check(doc.SelectionService.PrimarySelection?.Component is StackPanel, "Closing tag selects its own container");
                editor.Editor.CaretOffset = attributeOffset;
                editor.Editor.Document.Replace(exactText.IndexOf("Inspect > this", StringComparison.Ordinal), "Inspect > this".Length, "Updated button");
                await Task.Delay(750);
                Check(doc.SelectionService?.PrimarySelection?.Component is Button restored && Equals(restored.Content, "Updated button"),
                    "Selection survives editing and preview reload");
                editor.Editor.Text = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"200\">\r\n  <!-- keep this comment -->\r\n  <Button Content = ''  Width='120' Background=\"Red\" />\r\n</UserControl>";
                await Task.Delay(750);
                Click(doc.DesignContext.RootItem.View.GetVisualDescendants().OfType<Button>().First());
                await Task.Delay(100);
                var emptyContent = doc.Text;
                Commit(PropertyEditor("Content"), "It's <OK> & \"fine\"");
                Check(doc.Text == emptyContent.Replace("Content = ''", "Content = 'It&apos;s &lt;OK&gt; &amp; \"fine\"'"), "Empty attribute edit preserves quotes, CRLF, spacing and comments");
                await Task.Delay(750);
                var beforeWidth = doc.Text;
                Commit(PropertyEditor("Width"), "180");
                await Task.Delay(750);
                Check(doc.Text == beforeWidth.Replace("Width='120'", "Width='180'") && ((Button)doc.SelectionService!.PrimarySelection!.Component).Width == 180,
                    "Width changes source and rendered control");
                var beforeInvalid = doc.Text;
                Commit(PropertyEditor("Width"), "-5");
                Check(doc.Text == beforeInvalid && !doc.HasPreviewError, "Invalid property value leaves source and preview intact");
                var cancelledField = PropertyEditor("Width");
                cancelledField.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                Check(cancelledField.Text == "180", "Escape restores last committed value");
                Commit(PropertyEditor("Width"), "1,2");
                Check(doc.Text == beforeInvalid, "Numeric input incompatible with XAML converter is rejected");
                Commit(PropertyEditor("Width"), "Auto");
                await Task.Delay(750);
                Check(!doc.HasPreviewError && double.IsNaN(((Button)doc.SelectionService!.PrimarySelection!.Component).Width), "Auto width produces valid XAML and automatic sizing");
                var beforeControlChar = doc.Text;
                Commit(PropertyEditor("Content"), "bad\u0001text");
                Check(doc.Text == beforeControlChar && !doc.HasPreviewError, "Invalid XML characters are rejected without escaping the editor event");
                var beforeMargin = doc.Text;
                Commit(PropertyEditor("Margin"), "1,2,3,4");
                await Task.Delay(750);
                Check(doc.Text == beforeMargin.Replace("<Button ", "<Button Margin=\"1,2,3,4\" ") && ((Button)doc.SelectionService!.PrimarySelection!.Component).Margin == new Thickness(1, 2, 3, 4),
                    "Missing scalar attribute is inserted without rewriting element");
                var beforeColor = doc.Text;
                Commit(PropertyEditor("Background"), "#123456");
                await Task.Delay(750);
                Check(((Button)doc.SelectionService!.PrimarySelection!.Component).Background is Avalonia.Media.ISolidColorBrush brush && brush.Color == Avalonia.Media.Color.Parse("#123456"), "Color property updates preview");
                var afterColor = doc.Text;
                PropertyEditor("Background").RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Z, KeyModifiers = KeyModifiers.Control });
                Check(doc.Text == beforeColor, "Ctrl+Z in Properties undoes a committed edit");
                await Task.Delay(750);
                PropertyEditor("Background").RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Y, KeyModifiers = KeyModifiers.Control });
                Check(doc.Text == afterColor, "Ctrl+Y in Properties redoes a committed edit");
                await Task.Delay(750);
                var lostFocusField = PropertyEditor("Height");
                lostFocusField.Text = "60";
                lostFocusField.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(InputElement.LostFocusEvent));
                await Task.Delay(750);
                Check(((Button)doc.SelectionService!.PrimarySelection!.Component).Height == 60, "Leaving a property field commits its value");
                var staleField = PropertyEditor("Content");
                var beforeLiteral = doc.Text;
                Commit(PropertyEditor("Content"), "{literal}");
                await Task.Delay(750);
                Check(doc.Text.Contains("Content = '{}{literal}'") && Equals(((Button)doc.SelectionService!.PrimarySelection!.Component).Content, "{literal}"), "Literal braces do not create a markup extension");
                var sameText = doc.Text;
                Commit(PropertyEditor("Content"), "{literal}");
                Check(doc.Text == sameText, "Unchanged property preserves source");
                editor.Editor.Document.UndoStack.Undo();
                Check(doc.Text == beforeLiteral, "Unchanged property creates no undo entry");
                await Task.Delay(750);
                editor.Editor.Text = "<UserControl xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" Width=\"320\" Height=\"200\"><UserControl.Resources><SolidColorBrush x:Key=\"Accent\">Red</SolidColorBrush></UserControl.Resources><Button Content=\"{Binding Title}\" Background=\"{StaticResource Accent}\" /></UserControl>";
                await Task.Delay(750);
                Check(!doc.HasPreviewError, "Binding and resource fixture renders");
                var afterSourceEdit = doc.Text;
                Commit(staleField, "Old editor value");
                Check(doc.Text == afterSourceEdit, "Editor from a previous preview cannot overwrite newer source");
                editor.Editor.Select(doc.Text.IndexOf("<Button ", StringComparison.Ordinal) + 2, 0);
                await Task.Delay(100);
                var protectedSource = doc.Text;
                Check(PropertyEditor("Content").IsReadOnly && PropertyEditor("Background").IsReadOnly, "Bindings and resources remain read-only");
                Commit(PropertyEditor("Content"), "Overwrite");
                Commit(PropertyEditor("Background"), "Blue");
                Check(doc.Text == protectedSource, "Protected expressions cannot be overwritten through Properties");
                editor.Editor.Text = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"200\"><Button><Button.Content><TextBlock Text=\"Child\" /></Button.Content></Button></UserControl>";
                await Task.Delay(750);
                editor.Editor.Select(doc.Text.IndexOf("<Button>", StringComparison.Ordinal) + 2, 0);
                await Task.Delay(100);
                Check(PropertyEditor("Content").IsReadOnly, "Complex property element remains read-only");
                exactText = editor.Editor.Text;
                doc.Save();
                Check(File.ReadAllText(path) == exactText && !doc.IsDirty, "Save in Split preserves exact source");
                doc.Mode = DocumentMode.Xaml;
                Check(doc.Text == exactText, "Split to XAML does not serialize stale designer");
                doc.Mode = DocumentMode.Design;
                Check(doc.DesignSurface.IsEffectivelyEnabled, "Design mode restores interactive designer");
                Check(propertyGrid.IsEffectivelyEnabled && !propertyGrid.IsReadOnly, "Properties editing restored in Design mode");
                doc.Mode = split;
                editor.Editor.Text = "<UserControl";
                doc.Save();
                Check(File.ReadAllText(path) == "<UserControl", "Save before debounce writes latest editor content");
                await Task.Delay(750);
                Check(doc.HasPreviewError && view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == doc.PreviewError), "Invalid XAML shows preview error");
                Check(doc.SelectionService == null && propertyGrid.SelectedItems?.Any() != true, "Invalid preview clears Properties and selection");
                doc.Refresh();
                doc.Save();
                Check(doc.Text == "<UserControl" && File.ReadAllText(path) == doc.Text, "Invalid XAML survives preview refresh and save");
                doc.Mode = DocumentMode.Design;
                doc.Mode = split;
                Check(doc.Text == "<UserControl", "Invalid code survives round trip through Design mode");
                editor.Editor.Text = Markup("Recovered");
                await Task.Delay(750);
                Check(doc.DesignSurface.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Recovered"), "Preview recovers after invalid XAML is fixed");
                var grid = (Grid)splitter.Parent!;
                grid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
                grid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                doc.Mode = DocumentMode.Xaml;
                doc.Mode = split;
                Check(grid.ColumnDefinitions[0].Width.Value == 2 && grid.ColumnDefinitions[2].Width.Value == 1, "Split ratio survives mode changes");
                await Task.Delay(100);
                Click(doc.DesignSurface.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Recovered"));
                await Task.Delay(100); // Let ScrollTo rebuild editor visual lines before capturing.
                Directory.CreateDirectory("artifacts/SplitView");
                using (var screenshot = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(1100, 650)))
                {
                    screenshot.Render(window);
                    screenshot.Save("artifacts/SplitView/split.png");
                }
                var context = doc.DesignContext;
                editor.Editor.Text = Markup("Detached");
                window.Content = null;
                await Task.Delay(750);
                Check(ReferenceEquals(context, doc.DesignContext), "Detaching document cancels pending preview");
                window.Content = view;
                await Task.Delay(750);
                Check(doc.Mode == split && doc.DesignSurface.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Detached"), "Reattaching resumes preview without resetting mode");
                var codePath = Path.ChangeExtension(path, ".cs");
                try
                {
                    File.WriteAllText(codePath, "class Example {}");
                    var code = new Document(codePath);
                    code.Mode = split;
                    Check(code.Mode == DocumentMode.Xaml && code.Text == "class Example {}", "Non-XAML document remains editor only");
                }
                finally { File.Delete(codePath); }
                Console.WriteLine("TOTAL FAILURES: 0");
            }
            catch (Exception ex) { Console.WriteLine(ex); failed = true; }
            finally { propertiesWindow?.Close(); window?.Close(); File.Delete(path); stop.Cancel(); }
        });
        Dispatcher.UIThread.MainLoop(stop.Token);
        return failed ? 1 : 0;
    }
}
