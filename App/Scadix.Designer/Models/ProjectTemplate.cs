using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scadix.Designer;

// ── Data models ──────────────────────────────────────────────────────────────

public class ProjectTemplate
{
    public string Name        { get; set; } = "";
    public string ShortName   { get; set; } = "";   // dotnet new short name
    public string Description { get; set; } = "";
    public string Language    { get; set; } = "C#";
    public string Tags        { get; set; } = "";   // e.g. "Console"
    public string Category    { get; set; } = "";   // derived from Tags
}

public class TemplateCategory
{
    public string                 Name      { get; set; } = "";
    public string                 Icon      { get; set; } = "📁";
    public List<ProjectTemplate>  Templates { get; set; } = new();
}

public class TemplatePackage
{
    public string Name        { get; set; } = "";
    public string PackageId   { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status      { get; set; } = "Missing"; // Missing, Installing, Installed, Error
    public bool   IsInstalled => Status == "Installed";
}

// ── Dynamic loader ────────────────────────────────────────────────────────────

public static class TemplateCatalog
{
    private static List<TemplateCategory>? _cache;

    /// <summary>
    /// Load templates by running `dotnet new list` and parsing the output.
    /// Results are cached after the first call.
    /// </summary>
    public static List<TemplateCategory> Load(bool forceRefresh = false)
    {
        if (_cache != null && !forceRefresh)
            return _cache;

        var templates = RunDotnetNewList();
        _cache = GroupIntoCategories(templates);
        return _cache;
    }

    // ── Run `dotnet new list` ─────────────────────────────────────────────────

    private static List<ProjectTemplate> RunDotnetNewList()
    {
        var result = new List<ProjectTemplate>();
        try
        {
            var psi = new ProcessStartInfo("dotnet", "new list")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return FallbackTemplates();

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(15_000);

            result = ParseDotnetNewList(output);
        }
        catch
        {
            result = FallbackTemplates();
        }

        return result.Count > 0 ? result : FallbackTemplates();
    }

    // ── Parse output ──────────────────────────────────────────────────────────
    // dotnet new list output format:
    //
    // Template Name                    Short Name       Language    Tags
    // -------------------------------- ---------------- ----------- ----------------
    // Console App                      console          [C#],F#,VB  Common/Console
    // Class Library                    classlib         [C#],F#,VB  Common/Library

    private static List<ProjectTemplate> ParseDotnetNewList(string output)
    {
        var templates = new List<ProjectTemplate>();
        var lines     = output.Split('\n');

        // Find the separator line (all dashes)
        int dataStart = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i].Trim(), @"^[-\s]+$") && lines[i].Length > 10)
            {
                dataStart = i + 1;
                break;
            }
        }

        if (dataStart < 0) return templates;

        // Determine column positions from the separator line
        var sep = lines[dataStart - 1];
        var cols = Regex.Matches(sep, @"-+").Cast<Match>()
                        .Select(m => (start: m.Index, len: m.Length))
                        .ToList();

        if (cols.Count < 3) return templates;

        for (int i = dataStart; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string Extract(int colIdx)
            {
                if (colIdx >= cols.Count) return "";
                var (start, len) = cols[colIdx];
                if (start >= line.Length) return "";
                var end = Math.Min(start + len, line.Length);
                return line[start..end].Trim();
            }

            var name      = Extract(0);
            var shortName = Extract(1).Split(',')[0].Trim();
            var language  = Extract(2).Replace("[", "").Replace("]", "").Split(',')[0].Trim();
            var tags      = cols.Count > 3 ? Extract(3) : "";

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(shortName))
                continue;

            templates.Add(new ProjectTemplate
            {
                Name      = name,
                ShortName = shortName,
                Language  = language,
                Tags      = tags,
                Category  = DeriveCategory(tags, name)
            });
        }

        return templates;
    }

    // ── Derive category from tags ─────────────────────────────────────────────

    private static string DeriveCategory(string tags, string name)
    {
        var t = tags.ToLowerInvariant();
        var n = name.ToLowerInvariant();

        if (t.Contains("hmi") || n.Contains("scadix") || n.Contains("hmi")) return "HMI";
        if (t.Contains("uno") || n.Contains("uno"))             return "Uno Platform";
        if (t.Contains("avalonia") || n.Contains("avalonia"))   return "Avalonia UI";
        if (t.Contains("wpf")      || n.Contains("wpf"))        return "Desktop";
        if (t.Contains("winforms") || n.Contains("windows forms")) return "Desktop";
        if (t.Contains("maui")     || n.Contains("maui"))       return "MAUI";
        if (t.Contains("blazor")   || n.Contains("blazor"))     return "Web";
        if (t.Contains("web")      || t.Contains("mvc") || t.Contains("razor")) return "Web";
        if (t.Contains("console")  || n.Contains("console"))    return "Console";
        if (t.Contains("library")  || n.Contains("library"))    return "Library";
        if (t.Contains("test")     || n.Contains("test"))       return "Test";
        if (t.Contains("service")  || n.Contains("worker"))     return "Services";
        return "Other";
    }

    // ── Group into categories ─────────────────────────────────────────────────

    private static readonly string[] _categoryOrder =
    {
        "Uno Platform", "Avalonia UI", "Desktop", "Web", "Console",
        "Library", "MAUI", "Test", "Services", "Other"
    };

    private static readonly Dictionary<string, string> _categoryIcons = new()
    {
        ["Uno Platform"] = "🟣",
        ["Avalonia UI"] = "🔷",
        ["Desktop"]     = "🖥",
        ["Web"]         = "🌐",
        ["Console"]     = "⬛",
        ["Library"]     = "📦",
        ["MAUI"]        = "📱",
        ["Test"]        = "🧪",
        ["Services"]    = "⚙",
        ["Other"]       = "📄",
    };

    private static List<TemplateCategory> GroupIntoCategories(List<ProjectTemplate> templates)
    {
        var groups = templates
            .GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<TemplateCategory>();

        // 1. Add HMI Category first
        var parsedHmi = groups.GetValueOrDefault("HMI", new List<ProjectTemplate>());
        var hmiTemplates = new List<ProjectTemplate>();

        void AddHmiTemplate(string name, string shortName, string tags, string desc)
        {
            var existing = parsedHmi.FirstOrDefault(t => t.ShortName.Contains(shortName, StringComparison.OrdinalIgnoreCase) || t.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                hmiTemplates.Add(existing);
                parsedHmi.Remove(existing);
            }
            else
            {
                hmiTemplates.Add(new ProjectTemplate { Name = name, ShortName = shortName, Category = "HMI", Language = "C#", Tags = tags, Description = desc });
            }
        }

        AddHmiTemplate("Avalonia HMI App", "scadix.avalonia", "HMI/Avalonia", "A pre-configured Avalonia application with Designer and HMI libraries installed.");
        AddHmiTemplate("Avalonia MVVM HMI App", "scadix.avalonia.mvvm", "HMI/Avalonia", "A pre-configured Avalonia MVVM application with Designer and HMI libraries installed.");
        AddHmiTemplate("Avalonia Cross-Platform HMI App", "scadix.avalonia.xplat", "HMI/Avalonia", "A pre-configured Avalonia Cross-Platform application with Designer and HMI libraries installed.");
        AddHmiTemplate("WPF HMI App", "scadix.wpf", "HMI/WPF", "A pre-configured WPF application with Designer and HMI libraries installed.");
        AddHmiTemplate("MAUI HMI App", "scadix.maui", "HMI/MAUI", "A pre-configured MAUI application with Designer and HMI libraries installed.");
        AddHmiTemplate("Uno HMI App", "scadix.uno", "HMI/Uno", "A pre-configured Uno Platform application with Designer and HMI libraries installed.");

        hmiTemplates.AddRange(parsedHmi);

        result.Add(new TemplateCategory
        {
            Name = "HMI",
            Icon = "✨",
            Templates = hmiTemplates
        });
        
        groups.Remove("HMI");

        foreach (var cat in _categoryOrder)
        {
            if (!groups.TryGetValue(cat, out var list)) continue;
            result.Add(new TemplateCategory
            {
                Name      = cat,
                Icon      = _categoryIcons.GetValueOrDefault(cat, "📄"),
                Templates = list
            });
        }

        // Any remaining categories not in the order list
        foreach (var (cat, list) in groups)
        {
            if (_categoryOrder.Contains(cat)) continue;
            result.Add(new TemplateCategory
            {
                Name      = cat,
                Icon      = "📄",
                Templates = list
            });
        }

        return result;
    }

    // ── Fallback (when dotnet is not available) ───────────────────────────────

    private static List<ProjectTemplate> FallbackTemplates() => new()
    {
        new() { Name="Uno Platform App",          ShortName="unoapp",          Category="Uno Platform", Language="C#", Tags="Uno Platform/Mobile/Desktop/Web" },
        new() { Name="Avalonia Application",      ShortName="avalonia.app",    Category="Avalonia UI", Language="C#", Tags="Avalonia/Desktop" },
        new() { Name="Avalonia MVVM Application", ShortName="avalonia.mvvm",   Category="Avalonia UI", Language="C#", Tags="Avalonia/Desktop" },
        new() { Name="Console Application",       ShortName="console",         Category="Console",     Language="C#", Tags="Common/Console" },
        new() { Name="Class Library",             ShortName="classlib",        Category="Library",     Language="C#", Tags="Common/Library" },
        new() { Name="WPF Application",           ShortName="wpf",             Category="Desktop",     Language="C#", Tags="Common/WPF" },
        new() { Name="ASP.NET Core Web API",      ShortName="webapi",          Category="Web",         Language="C#", Tags="Web/WebAPI" },
        new() { Name="Blazor Server App",         ShortName="blazorserver",    Category="Web",         Language="C#", Tags="Web/Blazor" },
    };
}

// ── Package Manager ───────────────────────────────────────────────────────────

public static class TemplateManager
{
    private static readonly List<TemplatePackage> _essentialPackages = new()
    {
        new() { Name = "Uno Platform", PackageId = "Uno.Templates",      Description = "Official Uno Platform project templates." },
        new() { Name = "Avalonia UI",  PackageId = "Avalonia.Templates", Description = "Official Avalonia UI project templates." },
    };

    public static async Task<List<TemplatePackage>> GetPackagesStatusAsync()
    {
        // Force refresh to get latest list from dotnet
        var categories = await Task.Run(() => TemplateCatalog.Load(forceRefresh: true));
        var templates = categories.SelectMany(c => c.Templates).ToList();
        var results = new List<TemplatePackage>();

        foreach (var p in _essentialPackages)
        {
            bool installed = false;
            if (p.PackageId == "Uno.Templates")
                installed = templates.Any(t => t.ShortName == "unoapp" || t.ShortName == "unolib");
            else if (p.PackageId == "Avalonia.Templates")
                installed = templates.Any(t => t.ShortName == "avalonia.app" || t.ShortName == "avalonia.mvvm");

            results.Add(new TemplatePackage
            {
                Name        = p.Name,
                PackageId   = p.PackageId,
                Description = p.Description,
                Status      = installed ? "Installed" : "Missing"
            });
        }
        return results;
    }

    public static async Task<bool> InstallPackageAsync(string packageId)
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", $"new install {packageId}")
            {
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }
}
