using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Scadix.Designer;

/// <summary>
/// Loads all assemblies referenced by a .csproj into the Toolbox and MyTypeFinder.
/// Handles: project output, XAML namespace scan, ProjectReferences, PackageReferences, direct References.
/// </summary>
public class ProjectReferencesLoader
{
    // ── State ─────────────────────────────────────────────────────────────

    private string _projectPath = "";
    private string _csprojPath  = "";

    /// <summary>Assembly names already processed in this session (case-insensitive).</summary>
    private readonly HashSet<string> _processed = new(StringComparer.OrdinalIgnoreCase);

    // ── Excluded path segments (runtimes / ref / resources folders) ───────
    private static readonly string[] _excludedSegments = { "\\ref\\", "\\resources\\", "\\runtimes\\" };

    // ── Non-Windows target framework identifiers ──────────────────────────
    private static readonly string[] _nonWindowsTfms =
    {
        "android", "ios", "maccatalyst", "macos", "tvos",
        "tizen", "wasm", "browser", "linux", "unix"
    };

    // ── Non-Windows csproj SDK / OutputType hints ─────────────────────────
    private static readonly string[] _nonWindowsSdks =
    {
        "Microsoft.NET.Sdk.Android",
        "Microsoft.NET.Sdk.iOS",
        "Microsoft.NET.Sdk.MacCatalyst",
        "Microsoft.NET.Sdk.macOS",
        "Microsoft.NET.Sdk.tvOS",
        "Microsoft.NET.Sdk.Tizen"
    };

    // ── NuGet framework preference order (Windows only) ──────────────────
    private static readonly string[] _frameworks =
    {
        "net10.0-windows", "net10.0-windows", "net10.0-windows",
        "net7.0-windows",  "net6.0-windows",  "net5.0-windows",
        "net10.0", "net10.0", "net10.0", "net7.0", "net6.0",
        "net5.0", "netcoreapp3.1",
        "net48", "net472", "net471", "net47",
        "net462", "net461", "net46", "net45"
    };

    // ── XAML namespace regex ──────────────────────────────────────────────
    private static readonly Regex _nsRegex = new(
        @"(?:using\s+|xmlns:?(?:\w*)\s*=\s*""(?:clr-namespace:|using:))([^"";\s]+)(?:;assembly=[^""]+)?(?:""|;)?",
        RegexOptions.Compiled);

    // ═════════════════════════════════════════════════════════════════════
    // Public Entry Point
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Discover and load all assemblies for the project at <paramref name="projectPath"/>
    /// into <see cref="Toolbox"/> and <see cref="MyTypeFinder"/>.
    /// </summary>
    public void LoadAllReferences(string projectPath)
    {
        try
        {
            _projectPath = projectPath;
            _processed.Clear();

            var csprojFiles = Directory.GetFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly);
            if (csprojFiles.Length == 0) return;

            _csprojPath = csprojFiles[0];
            var doc = XDocument.Load(_csprojPath);

            // Skip non-Windows projects (Android, iOS, macOS, etc.)
            if (!IsWindowsProject(doc)) return;

            LoadProjectOutput();
            ScanXamlNamespaces();
            LoadProjectReferences(doc);
            LoadPackageReferences(doc);
            LoadDirectReferences(doc);
        }
        catch (Exception ex) { MainWindowViewModel.ReportException(ex); }
    }

    public void ClearProcessed() => _processed.Clear();

    // ═════════════════════════════════════════════════════════════════════
    // 1. Project Output
    // ═════════════════════════════════════════════════════════════════════

    private void LoadProjectOutput()
    {
        var projectName = Path.GetFileNameWithoutExtension(_csprojPath);
        var binFolder   = Path.Combine(_projectPath, "bin");

        if (!Directory.Exists(binFolder))
        {
            TryLoadFromAppDomain(projectName);
            return;
        }

        var dll = LatestFile(binFolder, $"{projectName}.dll");
        if (dll != null) LoadAssembly(dll);
        else             TryLoadFromAppDomain(projectName);
    }

    /// <summary>
    /// Fallback for unbuilt projects: find the assembly already loaded in AppDomain.
    /// </summary>
    private void TryLoadFromAppDomain(string projectName)
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => !a.IsDynamic &&
                string.Equals(a.GetName().Name, projectName, StringComparison.OrdinalIgnoreCase));

        if (asm == null) return;

        var name = asm.GetName().Name!;
        if (_processed.Contains(name) || IsInToolbox(name)) return;

        var controls = ExtractControls(asm);
        if (controls.Count == 0) return;

        MyTypeFinder.Instance.RegisterAssembly(asm);
        AddToToolbox(asm, asm.Location, controls);
        _processed.Add(name);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 2. XAML Namespace Scan
    // ═════════════════════════════════════════════════════════════════════

    private void ScanXamlNamespaces()
    {
        var xamlFiles = Directory.GetFiles(_projectPath, "*.xaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_projectPath, "*.axaml", SearchOption.AllDirectories))
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"));

        var namespaces = new HashSet<string>();

        foreach (var file in xamlFiles)
        {
            try
            {
                foreach (Match m in _nsRegex.Matches(File.ReadAllText(file)))
                {
                    var ns = m.Groups[1].Value;
                    if (!string.IsNullOrEmpty(ns) &&
                        !ns.StartsWith("System.") &&
                        !ns.StartsWith("Microsoft."))
                        namespaces.Add(ns);
                }
            }
            catch { /* skip unreadable file */ }
        }

        if (namespaces.Count > 0)
            LoadControlsMatchingNamespaces(namespaces);
    }

    private void LoadControlsMatchingNamespaces(HashSet<string> namespaces)
    {
        var binFolder = Path.Combine(_projectPath, "bin");
        if (!Directory.Exists(binFolder)) return;

        foreach (var dll in AllDlls(binFolder).OrderByDescending(File.GetLastWriteTime))
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(dll);
                if (_processed.Contains(fileName)) continue;

                var asm = Resolve(dll);
                if (asm == null) continue;

                var name = asm.GetName().Name!;
                if (IsInToolbox(name)) continue;

                var allTypes    = SafeGetTypes(asm);
                var hasMatch    = allTypes.Any(t => t?.Namespace != null && namespaces.Contains(t.Namespace));
                if (!hasMatch) continue;

                var controls = allTypes
                    .Where(t => t != null && IsControl(t) && t.Namespace != null && namespaces.Contains(t.Namespace))
                    .ToList();

                MyTypeFinder.Instance.RegisterAssembly(asm);
                _processed.Add(name);

                if (controls.Count > 0)
                    AddToToolbox(asm, dll, controls);
            }
            catch (Exception ex) { MainWindowViewModel.ReportException(ex); }
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // 3. ProjectReference
    // ═════════════════════════════════════════════════════════════════════

    private void LoadProjectReferences(XDocument doc)
    {
        foreach (var include in doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrEmpty(v)))
        {
            try
            {
                var refCsproj = Path.GetFullPath(
                    Path.Combine(Path.GetDirectoryName(_csprojPath)!, include!));

                if (!File.Exists(refCsproj)) continue;

                var refName   = Path.GetFileNameWithoutExtension(refCsproj);
                var refFolder = Path.GetDirectoryName(refCsproj)!;

                if (_processed.Contains(refName)) continue;

                // Skip non-Windows referenced projects
                var refDoc = XDocument.Load(refCsproj);
                if (!IsWindowsProject(refDoc)) continue;

                var dll = LatestFile(Path.Combine(refFolder, "bin"), $"{refName}.dll");
                if (dll != null) LoadAssembly(dll);

                // Load that project's own NuGet packages too
                LoadPackageReferences(refDoc, refFolder);
            }
            catch (Exception ex) { MainWindowViewModel.ReportException(ex); }
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // 4. PackageReference  (NuGet)
    // ═════════════════════════════════════════════════════════════════════

    private void LoadPackageReferences(XDocument doc) =>
        LoadPackageReferences(doc, _projectPath);

    private void LoadPackageReferences(XDocument doc, string baseFolder)
    {
        foreach (var pkg in doc.Descendants("PackageReference"))
        {
            try
            {
                var name    = pkg.Attribute("Include")?.Value;
                var version = pkg.Attribute("Version")?.Value ?? pkg.Element("Version")?.Value;
                if (!string.IsNullOrEmpty(name))
                    ResolveNuGetPackage(name, version, baseFolder);
            }
            catch (Exception ex) { MainWindowViewModel.ReportException(ex); }
        }
    }

    private void ResolveNuGetPackage(string packageName, string? version, string baseFolder)
    {
        // 1. Local packages folder (classic-style repos)
        var local = FindPackagesFolder(baseFolder);
        if (local != null)
        {
            var folder = Directory.GetDirectories(local, $"{packageName}*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(d => d).FirstOrDefault();
            if (folder != null) { LoadDllsFromPackage(folder); return; }
        }

        // 2. Global NuGet cache  (~/.nuget/packages)
        var cache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages", packageName.ToLower());

        if (!Directory.Exists(cache)) return;

        var versionFolder = string.IsNullOrEmpty(version)
            ? Directory.GetDirectories(cache).OrderByDescending(d => d).FirstOrDefault()
            : Path.Combine(cache, version);

        if (versionFolder != null && Directory.Exists(versionFolder))
            LoadDllsFromPackage(versionFolder);
    }

    private void LoadDllsFromPackage(string packageFolder)
    {
        var lib = Path.Combine(packageFolder, "lib");
        if (!Directory.Exists(lib)) return;

        var target = _frameworks
            .Select(fw => Path.Combine(lib, fw))
            .FirstOrDefault(Directory.Exists)
            ?? Directory.GetDirectories(lib).OrderByDescending(d => d).FirstOrDefault();

        if (target == null) return;

        foreach (var dll in Directory.GetFiles(target, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(f => !IsExcluded(f)))
            LoadAssembly(dll);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 5. Direct Reference (HintPath)
    // ═════════════════════════════════════════════════════════════════════

    private void LoadDirectReferences(XDocument doc)
    {
        foreach (var r in doc.Descendants("Reference").Where(x => x.Attribute("Include") != null))
        {
            try
            {
                var hint = r.Element("HintPath")?.Value;
                if (string.IsNullOrEmpty(hint)) continue;

                var full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_csprojPath)!, hint));
                if (File.Exists(full)) LoadAssembly(full);
            }
            catch (Exception ex) { MainWindowViewModel.ReportException(ex); }
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Core: LoadAssembly
    // ═════════════════════════════════════════════════════════════════════

    private void LoadAssembly(string dllPath)
    {
        try
        {
            if (IsExcluded(dllPath) || !File.Exists(dllPath)) return;

            var fileName = Path.GetFileNameWithoutExtension(dllPath);
            if (_processed.Contains(fileName) || IsInToolbox(fileName)) return;

            var asm = Resolve(dllPath);
            if (asm == null) return;

            MyTypeFinder.Instance.RegisterAssembly(asm);
            MyTypeFinder.Instance.SetProjectAssembly(asm);
            var controls = ExtractControls(asm);
            if (controls.Count > 0)
                AddToToolbox(asm, dllPath, controls);

            _processed.Add(fileName);
        }
        catch (Exception ex) { MainWindowViewModel.ReportException(ex); }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Private Helpers
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>Find the newest DLL matching <paramref name="pattern"/> under <paramref name="folder"/>.</summary>
    private static string? LatestFile(string folder, string pattern) =>
        Directory.Exists(folder)
            ? Directory.GetFiles(folder, pattern, SearchOption.AllDirectories)
                .Where(f => !IsExcluded(f))
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault()
            : null;

    /// <summary>All non-excluded DLLs under a folder.</summary>
    private static IEnumerable<string> AllDlls(string folder) =>
        Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f));

    /// <summary>Find assembly in AppDomain first, then load from disk.</summary>
    private static Assembly? Resolve(string dllPath)
    {
        var name = Path.GetFileNameWithoutExtension(dllPath);
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => !a.IsDynamic &&
                string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
            ?? (File.Exists(dllPath) ? Assembly.LoadFrom(dllPath) : null);
    }

    /// <summary>Get exported types, tolerating partial-load failures.</summary>
    private static Type[] SafeGetTypes(Assembly asm)
    {
        try { return asm.GetExportedTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).ToArray()!; }
        catch { return Array.Empty<Type>(); }
    }

    /// <summary>Extract all public, concrete, parameterless-constructor Controls.</summary>
    private static List<Type> ExtractControls(Assembly asm) =>
        SafeGetTypes(asm).Where(t => t != null && IsControl(t)).ToList();

    private static bool IsControl(Type t) =>
        !t.IsAbstract &&
        !t.IsGenericTypeDefinition &&
        t.IsSubclassOf(typeof(Control)) &&
        t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) != null;

    private static bool IsInToolbox(string assemblyName) =>
        Toolbox.Instance.AssemblyNodes.Any(n =>
            string.Equals(n.Assembly?.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

    private static void AddToToolbox(Assembly asm, string path, List<Type> controls)
    {
        var node = new AssemblyNode { Assembly = asm, Path = path };
        foreach (var t in controls)
            node.Controls.Add(new ControlNode { Type = t });
        node.Controls.Sort((a, b) => a.Name.CompareTo(b.Name));
        Toolbox.Instance.AssemblyNodes.Add(node);
    }

    /// <summary>True when the path is inside a ref / resources / runtimes / non-Windows TFM sub-folder.</summary>
    private static bool IsExcluded(string path) =>
        _excludedSegments.Any(seg => path.Contains(seg, StringComparison.OrdinalIgnoreCase)) ||
        IsNonWindowsPath(path);

    /// <summary>Walk up the directory tree looking for a "packages" folder.</summary>
    private static string? FindPackagesFolder(string start)
    {
        for (var dir = start; !string.IsNullOrEmpty(dir);)
        {
            var candidate = Path.Combine(dir, "packages");
            if (Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == dir) break;
            dir = parent;
        }
        return null;
    }

    /// <summary>
    /// Returns true if the project targets Windows (net*-windows, net4x, netcoreapp, etc.)
    /// Returns false for Android, iOS, macOS, Tizen, WASM, Linux projects.
    /// </summary>
    private static bool IsWindowsProject(XDocument csproj)
    {
        try
        {
            // Check SDK attribute on root Project element
            var sdk = csproj.Root?.Attribute("Sdk")?.Value ?? "";
            if (_nonWindowsSdks.Any(s => sdk.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Check TargetFramework / TargetFrameworks
            var tfms = csproj.Descendants("TargetFramework")
                .Concat(csproj.Descendants("TargetFrameworks"))
                .SelectMany(e => e.Value.Split(';'))
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            if (tfms.Count == 0) return true; // no TFM specified → assume Windows

            // If ALL tfms are non-Windows → skip
            // If at least one is Windows-compatible → load
            return tfms.Any(tfm =>
                !_nonWindowsTfms.Any(nw => tfm.Contains(nw)));
        }
        catch { return true; } // on parse error, don't skip
    }

    /// <summary>True when the DLL path contains a non-Windows TFM folder segment.</summary>
    private static bool IsNonWindowsPath(string path)
    {
        var lower = path.ToLowerInvariant();
        return _nonWindowsTfms.Any(nw => 
            lower.Contains($"\\{nw}") || lower.Contains($"/{nw}"));
    }
}
