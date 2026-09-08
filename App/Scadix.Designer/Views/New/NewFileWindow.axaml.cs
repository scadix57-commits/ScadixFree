using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Scadix.Designer;

// ── File template descriptor ──────────────────────────────────────────────────

public class FileTypeEntry
{
    public string Label      { get; set; } = "";
    public string Icon       { get; set; } = "📄";
    public string IconColor  { get; set; } = "#1F1F1F";
    public string Extension  { get; set; } = ".cs";
    public string Category   { get; set; } = "C#";
    public Func<string, string> Template { get; set; } = _ => "";
}

// ── Window ────────────────────────────────────────────────────────────────────

public partial class NewFileWindow : Window
{
    // Result — set when user clicks OK
    public string? CreatedFilePath { get; private set; }

    private readonly string          _targetDirectory;
    private readonly string          _namespace;
    private FileTypeEntry?           _selectedType;
    private List<FileTypeEntry>      _allTypes;

    public NewFileWindow(string targetDirectory)
    {
        _targetDirectory = targetDirectory;
        _namespace       = SolutionService.GetNamespace(targetDirectory);
        _allTypes        = BuildTypes(_namespace);
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        BuildTypeList(_allTypes);
        if (_allTypes.Count > 0)
            SelectType(_allTypes[0]);

        NameBox.Focus();
        NameBox.SelectAll();
    }

    // ── Type list ─────────────────────────────────────────────────────────────

    private void BuildTypeList(IEnumerable<FileTypeEntry> types)
    {
        TypeList.Children.Clear();

        string? lastCategory = null;
        foreach (var t in types)
        {
            // Category separator
            if (t.Category != lastCategory)
            {
                if (lastCategory != null)
                    TypeList.Children.Add(new Border
                    {
                        Height          = 1,
                        Background      = Brushes.LightGray,
                        Margin          = new Avalonia.Thickness(0)
                    });

                lastCategory = t.Category;
            }

            var btn = new Button { Classes = { "file-type" }, Tag = t };
            btn.Content = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing     = 10,
                Children    =
                {
                    new TextBlock
                    {
                        Text       = t.Icon,
                        Foreground = SolidColorBrush.Parse(t.IconColor),
                        FontWeight = FontWeight.Bold,
                        FontSize   = 13,
                        Width      = 22,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text      = t.Label,
                        FontSize  = 13,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    }
                }
            };
            btn.Click += (_, _) => SelectType(t);
            TypeList.Children.Add(btn);
        }
    }

    private void SelectType(FileTypeEntry t)
    {
        _selectedType = t;

        foreach (var child in TypeList.Children.OfType<Button>())
        {
            child.Classes.Remove("selected");
            if (child.Tag == t) child.Classes.Add("selected");
        }

        // Auto-set extension in name box if name is still default
        var current = NameBox.Text?.Trim() ?? "";
        var currentExt = Path.GetExtension(current).ToLowerInvariant();
        var knownExts  = _allTypes.Select(x => x.Extension.ToLowerInvariant()).Distinct().ToList();

        if (string.IsNullOrEmpty(current) ||
            knownExts.Contains(currentExt))
        {
            var baseName = string.IsNullOrEmpty(current)
                ? "NewFile"
                : Path.GetFileNameWithoutExtension(current);
            NameBox.Text = baseName + t.Extension;
            NameBox.SelectAll();
        }

        UpdateOkButton();
    }

    // ── Name box ──────────────────────────────────────────────────────────────

    private void NameBox_TextChanged(object? sender, TextChangedEventArgs e)
        => UpdateOkButton();

    private void NameBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && OkBtn.IsEnabled)
            CreateFile();
        else if (e.Key == Key.Escape)
            Close();
    }

    private void UpdateOkButton()
    {
        var name = NameBox.Text?.Trim() ?? "";
        OkBtn.IsEnabled = _selectedType != null &&
                          !string.IsNullOrEmpty(name) &&
                          IsValidFileName(name);
    }

    private static bool IsValidFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return name.IndexOfAny(invalid) < 0 && name.Length > 0;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    private void Ok_Click(object? sender, RoutedEventArgs e) => CreateFile();

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();

    private void CreateFile()
    {
        if (_selectedType == null) return;

        var name = NameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) return;

        // Ensure correct extension
        if (!name.EndsWith(_selectedType.Extension, StringComparison.OrdinalIgnoreCase))
            name += _selectedType.Extension;

        var filePath = Path.Combine(_targetDirectory, name);

        try
        {
            var content = _selectedType.Template(Path.GetFileNameWithoutExtension(name));
            File.WriteAllText(filePath, content);
            CreatedFilePath = filePath;
            Close();
        }
        catch (Exception ex)
        {
            MainWindowViewModel.ReportException(ex);
        }
    }

    // ── File type catalog ─────────────────────────────────────────────────────

   
    private static List<FileTypeEntry> BuildTypes(string ns = "MyApp") => new()
    {
        // ── C# ──────────────────────────────────────────────────────────────
        new FileTypeEntry
        {
            Label     = "Class",
            Icon      = "C#",
            IconColor = "#2E7D32",
            Extension = ".cs",
            Category  = "C#",
            Template  = name =>
                $"namespace {ns};\n\n" +
                $"public class {name}\n" +
                "{\n}\n"
        },
        new FileTypeEntry
        {
            Label     = "Interface",
            Icon      = "C#",
            IconColor = "#2E7D32",
            Extension = ".cs",
            Category  = "C#",
            Template  = name =>
                $"namespace {ns};\n\n" +
                $"public interface {name}\n" +
                "{\n}\n"
        },
        new FileTypeEntry
        {
            Label     = "Record",
            Icon      = "C#",
            IconColor = "#2E7D32",
            Extension = ".cs",
            Category  = "C#",
            Template  = name =>
                $"namespace {ns};\n\n" +
                $"public record {name}();\n"
        },
        new FileTypeEntry
        {
            Label     = "Struct",
            Icon      = "C#",
            IconColor = "#2E7D32",
            Extension = ".cs",
            Category  = "C#",
            Template  = name =>
                $"namespace {ns};\n\n" +
                $"public struct {name}\n" +
                "{\n}\n"
        },
        new FileTypeEntry
        {
            Label     = "Enum",
            Icon      = "C#",
            IconColor = "#2E7D32",
            Extension = ".cs",
            Category  = "C#",
            Template  = name =>
                $"namespace {ns};\n\n" +
                $"public enum {name}\n" +
                "{\n}\n"
        },

        // ── Avalonia ─────────────────────────────────────────────────────────
        new FileTypeEntry
        {
            Label     = "UserControl",
            Icon      = "AV",
            IconColor = "#1565C0",
            Extension = ".axaml",
            Category  = "Avalonia",
            Template  = name =>
                "<UserControl xmlns=\"https://github.com/avaloniaui\"\n" +
                "             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\n" +
                $"             x:Class=\"{ns}.{name}\">\n" +
                "    <Grid/>\n" +
                "</UserControl>\n"
        },
        new FileTypeEntry
        {
            Label     = "Window",
            Icon      = "AV",
            IconColor = "#1565C0",
            Extension = ".axaml",
            Category  = "Avalonia",
            Template  = name =>
                "<Window xmlns=\"https://github.com/avaloniaui\"\n" +
                "        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\n" +
                $"        x:Class=\"{ns}.{name}\"\n" +
                $"        Title=\"{name}\"\n" +
                "        Width=\"800\" Height=\"600\">\n" +
                "    <Grid/>\n" +
                "</Window>\n"
        },
        new FileTypeEntry
        {
            Label     = "Styles",
            Icon      = "AV",
            IconColor = "#1565C0",
            Extension = ".axaml",
            Category  = "Avalonia",
            Template  = _ =>
                "<Styles xmlns=\"https://github.com/avaloniaui\"\n" +
                "        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n\n" +
                "</Styles>\n"
        },
        new FileTypeEntry
        {
            Label     = "ResourceDictionary",
            Icon      = "AV",
            IconColor = "#1565C0",
            Extension = ".axaml",
            Category  = "Avalonia",
            Template  = _ =>
                "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\"\n" +
                "                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n\n" +
                "</ResourceDictionary>\n"
        },

        // ── Other ─────────────────────────────────────────────────────────────
        new FileTypeEntry
        {
            Label     = "JSON",
            Icon      = "{}",
            IconColor = "#E65100",
            Extension = ".json",
            Category  = "Other",
            Template  = _ => "{\n}\n"
        },
        new FileTypeEntry
        {
            Label     = "XML",
            Icon      = "</>",
            IconColor = "#6A1B9A",
            Extension = ".xml",
            Category  = "Other",
            Template  = _ => "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n</root>\n"
        },
        new FileTypeEntry
        {
            Label     = "Markdown",
            Icon      = "MD",
            IconColor = "#555555",
            Extension = ".md",
            Category  = "Other",
            Template  = name => $"# {name}\n\n"
        },
        new FileTypeEntry
        {
            Label     = "Text File",
            Icon      = "TXT",
            IconColor = "#888888",
            Extension = ".txt",
            Category  = "Other",
            Template  = _ => ""
        },
    };
}
