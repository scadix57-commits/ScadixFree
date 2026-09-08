using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Xaml;

namespace Scadix.Designer.Services
{
    public class EventHandlerService : IEventHandlerService
    {
        public void CreateEventHandler(DesignItemProperty eventProperty)
        {
            var designItem = eventProperty.DesignItem;
            var context = designItem.Context;
            string? axamlPath = null;

            // Try to get FilePath from XamlDesignContext if available
            if (context is XamlDesignContext xamlContext)
            {
                axamlPath = xamlContext.ParserSettings.FilePath;
            }

            // Fallback: Try to get XamlLoadSettings from services
            if (string.IsNullOrEmpty(axamlPath))
            {
                var settings = context.Services.GetService<XamlLoadSettings>();
                axamlPath = settings?.FilePath;
            }

            if (string.IsNullOrEmpty(axamlPath))
            {
                System.Diagnostics.Trace.WriteLine("[EventHandlerService] Could not determine AXAML file path.");
                return;
            }

            string csPath = axamlPath + ".cs";
            if (!File.Exists(csPath))
            {
                System.Diagnostics.Trace.WriteLine($"[EventHandlerService] Code-behind file not found: {csPath}");
                return;
            }

            string eventName = eventProperty.Name;
            // Use TextValue or ValueOnInstance as fallback
            string? handlerName = eventProperty.TextValue ?? eventProperty.ValueOnInstance?.ToString();

            if (string.IsNullOrEmpty(handlerName))
            {
                // Generate a default name if none exists (e.g. Button_Click)
                // Use Name if set, otherwise type name, otherwise "Control"
                string itemName = designItem.Name;
                if (string.IsNullOrEmpty(itemName))
                {
                    itemName = designItem.ComponentType.Name;
                }
                
                handlerName = $"{itemName}_{eventName}";
                eventProperty.SetValue(handlerName);
            }

            // Add the method to the C# file
            AddMethodToCodeBehind(csPath, handlerName);

            // Open the code-behind file in the designer
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                MainWindowViewModel.Instance.OpenFile(csPath);
            });
        }

        public DesignItemProperty? GetDefaultEvent(DesignItem item)
        {
            // Try to find common default events
            return item.Properties.FirstOrDefault(p => p.IsEvent && p.Name == "Click") 
                   ?? item.Properties.FirstOrDefault(p => p.IsEvent && p.Name == "PointerPressed")
                   ?? item.Properties.FirstOrDefault(p => p.IsEvent);
        }

        private void AddMethodToCodeBehind(string csPath, string handlerName)
        {
            try
            {
                string content = File.ReadAllText(csPath);

                // Basic check if the method already exists to avoid duplicates
                if (Regex.IsMatch(content, $@"void\s+{handlerName}\s*\("))
                    return;

                // Find the insertion point: usually before the last closing brace of the class
                int lastBrace = content.LastIndexOf('}');
                if (lastBrace == -1) return;

                int insertPos = lastBrace;

                // Check for file-scoped namespace: namespace MyNamespace;
                // If it's file-scoped, there's only one level of braces (the class).
                // If it's block-scoped, the last brace is the namespace closing brace.
                bool isFileScopedNamespace = content.Contains("namespace") && 
                                           Regex.IsMatch(content, @"namespace\s+[\w\.]+\s*;");

                if (!isFileScopedNamespace)
                {
                    // Block namespace: find the second to last closing brace
                    int secondToLastBrace = content.LastIndexOf('}', lastBrace - 1);
                    if (secondToLastBrace != -1)
                    {
                        insertPos = secondToLastBrace;
                    }
                }

                // Prepare the method stub
                // We assume Avalonia.Interactivity.RoutedEventArgs is used for most events.
                // In a more advanced implementation, we could look up the actual event type.
                string methodStub = $"\n\n    private void {handlerName}(object? sender, Avalonia.Interactivity.RoutedEventArgs e)\n    {{\n        \n    }}\n";
                
                content = content.Insert(insertPos, methodStub);
                
                File.WriteAllText(csPath, content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[EventHandlerService] Error adding method to {csPath}: {ex.Message}");
            }
        }
    }
}
