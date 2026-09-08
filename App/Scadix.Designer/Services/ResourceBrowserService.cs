using Avalonia.Media;
using Scadix.Designer.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Scadix.Designer.Services
{
    // ── Models ──────────────────────────────────────────────────────────────────

    public enum ResourceKind { Color, Brush, Style, ControlTheme, String, Number, Image, Other }

    public class ResourceItem
    {
        public string Key      { get; set; } = string.Empty;
        public string Source   { get; set; } = string.Empty;   // assembly / file name
        public ResourceKind Kind { get; set; }
        public object? Value   { get; set; }
        public string XamlRef  => $"{{StaticResource {Key}}}";

        // Preview helpers
        public bool   IsColor  => Kind == ResourceKind.Color || Kind == ResourceKind.Brush;
        public Color  PreviewColor { get; set; }
    }

    public class ResourceGroup
    {
        public string Name { get; set; } = string.Empty;
        public ResourceKind Kind { get; set; }
        public List<ResourceItem> Items { get; set; } = new();
    }

    // ── Service ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans project DLLs and Avalonia assemblies for design-time resources.
    /// </summary>
    public static class ResourceBrowserService
    {
        // ── Public API ───────────────────────────────────────────────────────────

        public static async Task<List<ResourceGroup>> ScanAsync(string? projectRoot = null)
        {
            return await Task.Run(() => Scan(projectRoot));
        }

        // ── Core Scan ────────────────────────────────────────────────────────────

        private static List<ResourceGroup> Scan(string? projectRoot)
        {
            var items = new List<ResourceItem>();

            // 1. Scan Avalonia built-in resources (Theme colors etc.)
            ScanAvaloniaTheme(items);

            if (!string.IsNullOrEmpty(projectRoot))
            {
                // 2. Scan project source files (axaml, images)
                ScanProjectFiles(projectRoot, items);

                // 3. Scan project assemblies from bin/
                ScanProjectAssemblies(projectRoot, items);
            }

            // 4. Scan app-domain loaded assemblies
            ScanLoadedAssemblies(items);

            return GroupItems(items);
        }

        // ── Project File Scanner ─────────────────────────────────────────────────

        private static void ScanProjectFiles(string projectRoot, List<ResourceItem> items)
        {
            try
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 10 };
                var rootDir = new DirectoryInfo(projectRoot);

                // Skip: bin, obj, .git, .vs
                bool ShouldSkip(DirectoryInfo dir) => 
                    dir.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    dir.Name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                    dir.Name.StartsWith(".");

                // 1. Scan XAML files for keys
                foreach (var file in rootDir.EnumerateFiles("*.axaml", options)
                                       .Concat(rootDir.EnumerateFiles("*.xaml", options)))
                {
                    if (file.Directory?.FullName.Split(Path.DirectorySeparatorChar).Any(p => p == "bin" || p == "obj" || p.StartsWith(".")) == true)
                        continue;

                    try
                    {
                        var content = File.ReadAllText(file.FullName);
                        ParseXamlForResources(content, file.Name, items);
                    }
                    catch { }
                }

                // 2. Scan for Image Assets
                var imageExts = new[] { ".png", ".jpg", ".jpeg", ".svg", ".ico", ".bmp", ".gif" };
                foreach (var file in rootDir.EnumerateFiles("*.*", options))
                {
                    if (file.Directory?.FullName.Split(Path.DirectorySeparatorChar).Any(p => p == "bin" || p == "obj" || p.StartsWith(".")) == true)
                        continue;

                    if (imageExts.Contains(file.Extension.ToLowerInvariant()))
                    {
                        var relativePath = Path.GetRelativePath(projectRoot, file.FullName);
                        if (!items.Any(i => i.Key == relativePath))
                        {
                            items.Add(new ResourceItem
                            {
                                Key = relativePath,
                                Source = "Project Assets",
                                Kind = ResourceKind.Image,
                                Value = file.FullName // Store full path as value
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceBrowser] File scan error: {ex.Message}");
            }
        }

        // ── Avalonia Theme Scanner ────────────────────────────────────────────────

        private static void ScanAvaloniaTheme(List<ResourceItem> items)
        {
            try
            {
                var app = Avalonia.Application.Current;
                if (app?.Resources == null) return;

                ScanResourceDictionary(app.Resources, "Avalonia Theme", items);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceBrowser] Theme scan error: {ex.Message}");
            }
        }

        // ── Project Assembly Scanner ──────────────────────────────────────────────

        private static void ScanProjectAssemblies(string projectRoot, List<ResourceItem> items)
        {
            try
            {
                var binDirs = new[]
                {
                    Path.Combine(projectRoot, "bin", "Debug"),
                    Path.Combine(projectRoot, "bin", "Release")
                };

                foreach (var binDir in binDirs.Where(Directory.Exists))
                {
                    // Include TFM sub-dirs (net10.0, net10.0, etc.)
                    var searchDirs = Directory.GetDirectories(binDir)
                        .SelectMany(d => new[] { d })
                        .Prepend(binDir);

                    foreach (var dir in searchDirs)
                    {
                        foreach (var dll in Directory.GetFiles(dir, "*.dll"))
                        {
                            TryLoadAndScanAssembly(dll, items);
                        }
                    }
                    break; // Only scan Debug or first found
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceBrowser] Project scan error: {ex.Message}");
            }
        }

        private static void TryLoadAndScanAssembly(string dllPath, List<ResourceItem> items)
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(dllPath);

                // Skip well-known framework assemblies to keep results clean
                if (IsFrameworkAssembly(name)) return;

                var asm = AssemblyDiscoveryService
                              .LoadAssemblyFromPath(dllPath);
                if (asm == null) return;

                ScanAssemblyResources(asm, name, items);
            }
            catch { /* silently skip unloadable assemblies */ }
        }

        // ── AppDomain Scanner ────────────────────────────────────────────────────

        private static void ScanLoadedAssemblies(List<ResourceItem> items)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.IsDynamic) continue;
                    var name = asm.GetName().Name ?? string.Empty;
                    if (IsFrameworkAssembly(name)) continue;

                    ScanAssemblyResources(asm, name, items);
                }
                catch { }
            }
        }

        // ── Assembly Resource Scanner ─────────────────────────────────────────────

        private static void ScanAssemblyResources(Assembly asm, string sourceName, List<ResourceItem> items)
        {
            // Look for XAML embedded resources
            var resourceNames = asm.GetManifestResourceNames()
                .Where(n => n.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
                            n.EndsWith(".xaml",  StringComparison.OrdinalIgnoreCase));

            foreach (var resName in resourceNames)
            {
                try
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream == null) continue;

                    using var reader = new StreamReader(stream);
                    var xaml = reader.ReadToEnd();

                    ParseXamlForResources(xaml, sourceName, items);
                }
                catch { }
            }
        }

        // ── XAML Parser ──────────────────────────────────────────────────────────

        private static void ParseXamlForResources(string xaml, string source, List<ResourceItem> items)
        {
            // Simple text-based extraction (no full XAML parse to keep it light)
            // Extracts: Color, SolidColorBrush, Style, ControlTheme with x:Key
            var lines = xaml.Split('\n');

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Extract x:Key
                var key = ExtractAttribute(trimmed, "x:Key");
                if (string.IsNullOrEmpty(key)) continue;

                var item = new ResourceItem { Key = key, Source = source };

                if (trimmed.StartsWith("<Color") || trimmed.Contains("Color x:Key"))
                {
                    item.Kind = ResourceKind.Color;
                    var colorVal = ExtractColorValue(trimmed);
                    if (colorVal.HasValue) item.PreviewColor = colorVal.Value;
                }
                else if (trimmed.Contains("SolidColorBrush") || trimmed.Contains("Brush"))
                {
                    item.Kind = ResourceKind.Brush;
                    var colorStr = ExtractAttribute(trimmed, "Color");
                    if (!string.IsNullOrEmpty(colorStr) && Color.TryParse(colorStr, out var c))
                        item.PreviewColor = c;
                }
                else if (trimmed.Contains("ControlTheme"))
                {
                    item.Kind = ResourceKind.ControlTheme;
                }
                else if (trimmed.StartsWith("<Style"))
                {
                    item.Kind = ResourceKind.Style;
                }
                else if (trimmed.Contains("x:String") || trimmed.Contains("sys:String"))
                {
                    item.Kind = ResourceKind.String;
                }
                else if (trimmed.Contains("StreamGeometry") || trimmed.Contains("Geometry"))
                {
                    item.Kind = ResourceKind.Other;
                }
                else
                {
                    item.Kind = ResourceKind.Other;
                }

                // Avoid duplicates by key+source
                if (!items.Any(i => i.Key == key && i.Source == source))
                    items.Add(item);
            }
        }

        // ── Resource Dictionary Scanner ──────────────────────────────────────────

        private static void ScanResourceDictionary(
            Avalonia.Controls.IResourceDictionary dict, string source, List<ResourceItem> items)
        {
            foreach (var kvp in dict)
            {
                try
                {
                    var key = kvp.Key?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(key) || items.Any(i => i.Key == key)) continue;

                    var item = new ResourceItem { Key = key, Source = source };
                    ClassifyValue(kvp.Value, item);
                    items.Add(item);
                }
                catch { }
            }

            // Recurse into merged dictionaries
            foreach (var merged in dict.MergedDictionaries)
            {
                try
                {
                    if (merged is Avalonia.Controls.IResourceDictionary mergedDict)
                        ScanResourceDictionary(mergedDict, source, items);
                }
                catch { }
            }
        }

        private static void ClassifyValue(object? value, ResourceItem item)
        {
            switch (value)
            {
                case Color c:
                    item.Kind = ResourceKind.Color;
                    item.PreviewColor = c;
                    item.Value = value;
                    break;
                case ISolidColorBrush b:
                    item.Kind = ResourceKind.Brush;
                    item.PreviewColor = b.Color;
                    item.Value = value;
                    break;
                case IBrush:
                    item.Kind = ResourceKind.Brush;
                    item.Value = value;
                    break;
                case string:
                    item.Kind = ResourceKind.String;
                    item.Value = value;
                    break;
                case double:
                case float:
                case int:
                    item.Kind = ResourceKind.Number;
                    item.Value = value;
                    break;
                default:
                    item.Kind = ResourceKind.Other;
                    item.Value = value;
                    break;
            }
        }

        // ── Grouping ─────────────────────────────────────────────────────────────

        private static List<ResourceGroup> GroupItems(List<ResourceItem> items)
        {
            var groups = new List<ResourceGroup>();

            var kinds = new[]
            {
                ("🎨 Colors",       ResourceKind.Color),
                ("🖌 Brushes",      ResourceKind.Brush),
                ("✨ Styles",       ResourceKind.Style),
                ("🎛 Control Themes", ResourceKind.ControlTheme),
                ("🖼️ Images",      ResourceKind.Image),
                ("📝 Strings",      ResourceKind.String),
                ("📏 Numbers",      ResourceKind.Number),
                ("📦 Other",        ResourceKind.Other),
            };

            foreach (var (name, kind) in kinds)
            {
                var groupItems = items.Where(i => i.Kind == kind).ToList();
                if (groupItems.Count > 0)
                    groups.Add(new ResourceGroup { Name = name, Kind = kind, Items = groupItems });
            }

            return groups;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string ExtractAttribute(string xml, string attrName)
        {
            var search = $"{attrName}=\"";
            var idx = xml.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return string.Empty;
            var start = idx + search.Length;
            var end = xml.IndexOf('"', start);
            return end < 0 ? string.Empty : xml[start..end];
        }

        private static Color? ExtractColorValue(string xml)
        {
            // Handles: <Color x:Key="...">#RRGGBB</Color>  or Color="..."
            var inlineColor = ExtractAttribute(xml, "Color");
            if (!string.IsNullOrEmpty(inlineColor) && Color.TryParse(inlineColor, out var c1))
                return c1;

            // Try text content between tags
            var start = xml.IndexOf('>');
            var end   = xml.LastIndexOf('<');
            if (start >= 0 && end > start)
            {
                var text = xml[(start + 1)..end].Trim();
                if (Color.TryParse(text, out var c2)) return c2;
            }
            return null;
        }

        private static readonly HashSet<string> _frameworkPrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Microsoft", "Avalonia", "netstandard", "mscorlib",
            "WindowsBase", "PresentationCore", "Dock", "AvaloniaEdit",
            "CommunityToolkit", "ReactiveUI", "DynamicData"
        };

        private static bool IsFrameworkAssembly(string name) =>
            _frameworkPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }
}
