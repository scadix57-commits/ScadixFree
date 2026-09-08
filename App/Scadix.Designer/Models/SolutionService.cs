using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Scadix.Designer;

/// <summary>
/// Parses .sln / .slnx / .csproj files and builds a SolutionNode tree
/// that mirrors the Visual Studio Solution Explorer layout.
/// </summary>
public static class SolutionService
{
    // ── Public entry point ───────────────────────────────────────────────

    public static SolutionNode Load(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".sln" => LoadSln(path),
            ".slnx" => LoadSlnx(path),
            ".csproj" => LoadStandaloneCsproj(path),
            _ => LoadFolder(Path.GetDirectoryName(path) ?? path)
        };
    }

    // ── .sln ─────────────────────────────────────────────────────────────

    private static SolutionNode LoadSln(string slnPath)
    {
        var slnDir = Path.GetDirectoryName(slnPath)!;
        var slnName = Path.GetFileNameWithoutExtension(slnPath);

        // Collect all project entries
        var projectRegex = new Regex(
            @"Project\(""\{(?<typeGuid>[^}]+)\}""\)\s*=\s*""(?<name>[^""]+)""\s*,\s*""(?<rel>[^""]+)""\s*,\s*""\{(?<projGuid>[^}]+)\}""",
            RegexOptions.Compiled);

        // Solution folder GUID
        const string SolutionFolderTypeGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

        var lines = File.ReadAllLines(slnPath);

        // First pass: collect all entries
        var allEntries = new List<(string typeGuid, string name, string rel, string projGuid)>();
        foreach (var line in lines)
        {
            var m = projectRegex.Match(line);
            if (m.Success)
                allEntries.Add((
                    m.Groups["typeGuid"].Value.ToUpperInvariant(),
                    m.Groups["name"].Value,
                    m.Groups["rel"].Value,
                    m.Groups["projGuid"].Value.ToUpperInvariant()));
        }

        // Parse NestedProjects section to build folder hierarchy
        var nestedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inNested = false;
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("GlobalSection(NestedProjects)")) { inNested = true; continue; }
            if (inNested && line.TrimStart().StartsWith("EndGlobalSection")) { inNested = false; continue; }
            if (inNested)
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    var child = parts[0].Trim().Trim('{', '}').ToUpperInvariant();
                    var parent = parts[1].Trim().Trim('{', '}').ToUpperInvariant();
                    nestedMap[child] = parent;
                }
            }
        }

        // Build node map
        var nodeMap = new Dictionary<string, SolutionNode>(StringComparer.OrdinalIgnoreCase);

        // Count real projects for badge
        int projectCount = allEntries.Count(e =>
            !e.typeGuid.Equals(SolutionFolderTypeGuid, StringComparison.OrdinalIgnoreCase));

        var root = new SolutionNode
        {
            Kind = SolutionNodeKind.Solution,
            Name = $"Solution '{slnName}'",
            Badge = $"({projectCount} project{(projectCount != 1 ? "s" : "")})",
            FilePath = slnPath
        };

        // Create solution folder nodes
        foreach (var (typeGuid, name, _, projGuid) in allEntries)
        {
            if (typeGuid == SolutionFolderTypeGuid)
            {
                // Clean name: remove leading/trailing slashes that some .sln files include
                var cleanName = name.Trim('/', '\\').Split('/', '\\').Last();
                var folderNode = new SolutionNode
                {
                    Kind = SolutionNodeKind.SolutionFolder,
                    Name = string.IsNullOrEmpty(cleanName) ? name : cleanName
                };
                nodeMap[projGuid] = folderNode;
            }
        }

        // Create project nodes
        foreach (var (typeGuid, name, rel, projGuid) in allEntries)
        {
            if (typeGuid == SolutionFolderTypeGuid) continue;

            var projRel = rel.Replace('\\', Path.DirectorySeparatorChar);
            var projPath = Path.GetFullPath(Path.Combine(slnDir, projRel));

            SolutionNode projNode;
            if (File.Exists(projPath) &&
                (projRel.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                 projRel.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                 projRel.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)))
            {
                projNode = CreateLazyProjectNode(name, projPath);
            }
            else
            {
                projNode = new SolutionNode
                {
                    Kind = SolutionNodeKind.Project,
                    Name = name,
                    FilePath = projPath,
                    IsMissing = true,
                    Tooltip = $"Unable to find project '{projPath}'"
                };
            }
            nodeMap[projGuid] = projNode;
        }

        // Wire hierarchy using NestedProjects
        var attached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (childGuid, parentGuid) in nestedMap)
        {
            if (nodeMap.TryGetValue(childGuid, out var childNode) &&
                nodeMap.TryGetValue(parentGuid, out var parentNode))
            {
                parentNode.Children.Add(childNode);
                attached.Add(childGuid);
            }
        }

        // Attach top-level nodes to root
        foreach (var (typeGuid, _, _, projGuid) in allEntries)
        {
            if (!attached.Contains(projGuid) && nodeMap.TryGetValue(projGuid, out var node))
                root.Children.Add(node);
        }

        return root;
    }

    // ── .slnx ────────────────────────────────────────────────────────────

    private static SolutionNode LoadSlnx(string slnxPath)
    {
        var slnDir = Path.GetDirectoryName(slnxPath)!;
        var slnName = Path.GetFileNameWithoutExtension(slnxPath);

        var root = new SolutionNode
        {
            Kind = SolutionNodeKind.Solution,
            Name = $"Solution '{slnName}'",
            FilePath = slnxPath
        };

        try
        {
            var xml = XDocument.Load(slnxPath);
            var ns = xml.Root?.Name.Namespace ?? XNamespace.None;

            // Recursive helper to process <Folder> and <Project> elements
            void ProcessElement(XElement el, SolutionNode parent)
            {
                if (el.Name.LocalName == "Folder")
                {
                    var folderName = el.Attribute("Name")?.Value ?? el.Attribute("Path")?.Value ?? "Folder";
                    // Clean name: remove leading/trailing slashes that some .sln files include
                    folderName = folderName.Trim('/', '\\').Split('/', '\\').Last();
                    var folderNode = new SolutionNode
                    {
                        Kind = SolutionNodeKind.SolutionFolder,
                        Name = folderName,
                        Parent = parent
                    };
                    parent.Children.Add(folderNode);
                    foreach (var child in el.Elements())
                        ProcessElement(child, folderNode);
                }
                else if (el.Name.LocalName == "Project")
                {
                    var relPath = el.Attribute("Path")?.Value;
                    if (string.IsNullOrEmpty(relPath)) return;

                    relPath = relPath.Replace('\\', Path.DirectorySeparatorChar);
                    var projPath = Path.GetFullPath(Path.Combine(slnDir, relPath));
                    var projName = Path.GetFileNameWithoutExtension(projPath);

                    var projNode = File.Exists(projPath)
                        ? CreateLazyProjectNode(projName, projPath)
                        : new SolutionNode 
                        { 
                            Kind = SolutionNodeKind.Project, 
                            Name = projName, 
                            FilePath = projPath,
                            IsMissing = true,
                            Tooltip = $"Unable to find project '{projPath}'"
                        };

                    parent.Children.Add(projNode);
                }
            }

            foreach (var el in xml.Root?.Elements() ?? Enumerable.Empty<XElement>())
                ProcessElement(el, root);

            root.Badge = $"({root.Children.Count} project{(root.Children.Count != 1 ? "s" : "")})";
        }
        catch (Exception ex)
        {
            root.Children.Add(new SolutionNode
            {
                Kind = SolutionNodeKind.Unknown,
                Name = $"⚠ {ex.Message}"
            });
        }

        return root;
    }

    // ── Standalone .csproj ───────────────────────────────────────────────

    private static SolutionNode LoadStandaloneCsproj(string csprojPath)
    {
        var name = Path.GetFileNameWithoutExtension(csprojPath);
        var proj = BuildCsprojNode(name, csprojPath);

        // Wrap in a minimal solution root
        var root = new SolutionNode
        {
            Kind = SolutionNodeKind.Solution,
            Name = $"Solution '{name}'",
            Badge = "(1 project)"
        };
        root.Children.Add(proj);
        return root;
    }

    // ── Core: build a project node from .csproj ──────────────────────────

    // Creates a project node that loads its children on first expand
    private static SolutionNode CreateLazyProjectNode(string name, string csprojPath)
    {
        var projNode = new SolutionNode
        {
            Kind = SolutionNodeKind.Project,
            Name = name,
            FilePath = csprojPath
        };

        // Placeholder to show expand arrow immediately
        var placeholder = new SolutionNode { Kind = SolutionNodeKind.Unknown, Name = "" };
        projNode.Children.Add(placeholder);

        projNode.OnExpanded = node =>
        {
            if (node.IsLoaded) return;
            node.IsLoaded = true;
            node.Children.Remove(placeholder);

            Task.Run(() => BuildCsprojNode(name, csprojPath))
                .ContinueWith(t =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var child in t.Result.Children)
                            node.Children.Add(child);
                        node.Badge = t.Result.Badge;
                    });
                });
        };

        return projNode;
    }

    // Builds a lightweight project node for use inside ProjectRefsGroup (no file tree, no recursion)
    private static SolutionNode BuildProjectRefNode(string name, string csprojPath)
    {
        var projDir = Path.GetDirectoryName(csprojPath)!;
        var tfm = ReadTargetFramework(csprojPath);

        var projNode = new SolutionNode
        {
            Kind = SolutionNodeKind.Project,
            Name = name,
            Badge = tfm,
            FilePath = csprojPath
        };

        var depsNode = new SolutionNode { Kind = SolutionNodeKind.DependenciesGroup, Name = "Dependencies" };

        depsNode.Children.Add(new SolutionNode { Kind = SolutionNodeKind.AnalyzersGroup, Name = "Analyzers" });

        var fwNode = new SolutionNode { Kind = SolutionNodeKind.FrameworksGroup, Name = "Frameworks" };
        if (!string.IsNullOrEmpty(tfm))
            fwNode.Children.Add(new SolutionNode { Kind = SolutionNodeKind.Package, Name = tfm });
        depsNode.Children.Add(fwNode);

        var (packages, projectRefs) = ReadDependencies(csprojPath, projDir);

        var pkgNode = new SolutionNode { Kind = SolutionNodeKind.PackagesGroup, Name = "Packages" };
        foreach (var (pkgName, version) in packages.OrderBy(p => p.name))
            pkgNode.Children.Add(new SolutionNode { Kind = SolutionNodeKind.Package, Name = pkgName, Badge = version });
        depsNode.Children.Add(pkgNode);

        if (projectRefs.Count > 0)
        {
            var projRefsNode = new SolutionNode { Kind = SolutionNodeKind.ProjectRefsGroup, Name = "Projects" };
            foreach (var (refName, refPath) in projectRefs.OrderBy(r => r.name))
            {
                var isMissing = !File.Exists(refPath);
                if (isMissing)
                {
                    projRefsNode.Children.Add(new SolutionNode
                    {
                        Kind = SolutionNodeKind.ProjectRef,
                        Name = refName,
                        FilePath = refPath,
                        IsMissing = true,
                        Tooltip = $"Unable to find project '{refPath}'"
                    });
                }
                else
                {
                    projRefsNode.Children.Add(CreateLazyProjectNode(refName, refPath));
                }
            }
            depsNode.Children.Add(projRefsNode);
        }

        projNode.Children.Add(depsNode);
        return projNode;
    }

    // Detects package version conflicts between this project and its references (NU1605 style)
    private static (bool hasConflict, string conflictTooltip) DetectPackageConflicts(
        string csprojPath, string projDir,
        List<(string name, string version)> packages)
    {
        // Build a map: packageName -> list of (version, source)
        var versionMap = new Dictionary<string, List<(string ver, string source)>>(StringComparer.OrdinalIgnoreCase);

        void AddPkg(string pkg, string ver, string source)
        {
            if (string.IsNullOrEmpty(pkg) || string.IsNullOrEmpty(ver)) return;
            if (!versionMap.TryGetValue(pkg, out var list))
                versionMap[pkg] = list = new();
            if (!list.Any(x => x.ver == ver))
                list.Add((ver, source));
        }

        var projName = Path.GetFileNameWithoutExtension(csprojPath);
        foreach (var (pkg, ver) in packages)
            AddPkg(pkg, ver, projName);

        // One level deep: collect packages from each project reference
        var (_, projectRefs) = ReadDependencies(csprojPath, projDir);
        foreach (var (refName, refPath) in projectRefs)
        {
            if (!File.Exists(refPath)) continue;
            var refDir = Path.GetDirectoryName(refPath)!;
            var (refPkgs, _) = ReadDependencies(refPath, refDir);
            foreach (var (pkg, ver) in refPkgs)
                AddPkg(pkg, ver, refName);
        }

        var conflicts = versionMap
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => $"{kv.Key}: " + string.Join(" / ", kv.Value.Select(x => $"{x.ver} ({x.source})")))
            .ToList();

        return conflicts.Count > 0
            ? (true, "Package version conflicts:\n" + string.Join("\n", conflicts))
            : (false, "");
    }

    public static SolutionNode BuildCsprojNode(string name, string csprojPath)
    {
        var projDir = Path.GetDirectoryName(csprojPath)!;
        var tfm = ReadTargetFramework(csprojPath);

        var projNode = new SolutionNode
        {
            Kind = SolutionNodeKind.Project,
            Name = name,
            Badge = tfm,
            FilePath = csprojPath
        };

        // ── Dependencies ─────────────────────────────────────────────────
        var depsNode = new SolutionNode
        {
            Kind = SolutionNodeKind.DependenciesGroup,
            Name = "Dependencies"
        };

        // Analyzers (always present, static)
        depsNode.Children.Add(new SolutionNode
        {
            Kind = SolutionNodeKind.AnalyzersGroup,
            Name = "Analyzers"
        });

        // Frameworks
        var fwNode = new SolutionNode
        {
            Kind = SolutionNodeKind.FrameworksGroup,
            Name = "Frameworks"
        };
        if (!string.IsNullOrEmpty(tfm))
            fwNode.Children.Add(new SolutionNode
            {
                Kind = SolutionNodeKind.Package,
                Name = tfm,
                Badge = ""
            });
        depsNode.Children.Add(fwNode);

        // Packages (NuGet)
        var (packages, projectRefs) = ReadDependencies(csprojPath, projDir);

        var pkgNode = new SolutionNode
        {
            Kind = SolutionNodeKind.PackagesGroup,
            Name = "Packages"
        };
        foreach (var (pkgName, version) in packages.OrderBy(p => p.name))
            pkgNode.Children.Add(new SolutionNode
            {
                Kind = SolutionNodeKind.Package,
                Name = pkgName,
                Badge = version
            });
        depsNode.Children.Add(pkgNode);

        // Detect package version conflicts
        var (hasConflict, conflictTooltip) = DetectPackageConflicts(csprojPath, projDir, packages);
        if (hasConflict)
        {
            projNode.HasWarning = true;
            projNode.Tooltip = conflictTooltip;
        }

        // Project references
        if (projectRefs.Count > 0)
        {
            var projRefsNode = new SolutionNode
            {
                Kind = SolutionNodeKind.ProjectRefsGroup,
                Name = "Projects"
            };
            foreach (var (refName, refPath) in projectRefs.OrderBy(r => r.name))
            {
                if (File.Exists(refPath))
                {
                    var refNode = BuildProjectRefNode(refName, refPath);
                    projRefsNode.Children.Add(refNode);
                }
                else
                {
                    projRefsNode.Children.Add(new SolutionNode
                    {
                        Kind = SolutionNodeKind.ProjectRef,
                        Name = refName,
                        FilePath = refPath,
                        IsMissing = true,
                        Tooltip = $"Unable to find project '{refPath}'"
                    });
                }
            }
           
            depsNode.Children.Add(projRefsNode);
        }

        projNode.Children.Add(depsNode);

        // ── File tree ─────────────────────────────────────────────────────
        AddDirectoryContents(projNode, projDir);

        return projNode;
    }

    // ── Parse csproj for packages and project references ─────────────────

    private static (List<(string name, string version)> packages,
                    List<(string name, string path)> projectRefs)
        ReadDependencies(string csprojPath, string projDir)
    {
        var packages = new List<(string, string)>();
        var projectRefs = new List<(string, string)>();

        try
        {
            var xml = XDocument.Load(csprojPath);

            // PackageReference
            var centralVersions = GetCentralPackageVersions(projDir);
            foreach (var el in xml.Descendants("PackageReference"))
            {
                var pkgName = el.Attribute("Include")?.Value ?? "";
                var version = el.Attribute("Version")?.Value
                           ?? el.Element("Version")?.Value
                           ?? "";
                
                if (string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(pkgName))
                {
                    centralVersions.TryGetValue(pkgName, out version);
                }

                if (!string.IsNullOrEmpty(pkgName))
                    packages.Add((pkgName, version ?? ""));
            }

            // ProjectReference
            foreach (var el in xml.Descendants("ProjectReference"))
            {
                var rel = el.Attribute("Include")?.Value ?? "";
                if (string.IsNullOrEmpty(rel)) continue;

                rel = rel.Replace('\\', Path.DirectorySeparatorChar);
                var absPath = Path.GetFullPath(Path.Combine(projDir, rel));
                var refName = Path.GetFileNameWithoutExtension(absPath);
                projectRefs.Add((refName, absPath));
            }
        }
        catch { /* ignore parse errors */ }

        return (packages, projectRefs);
    }

    private static Dictionary<string, string> GetCentralPackageVersions(string projDir)
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dir = projDir;
        while (!string.IsNullOrEmpty(dir))
        {
            var propsPath = Path.Combine(dir, "Directory.Packages.props");
            if (File.Exists(propsPath))
            {
                try
                {
                    var xml = XDocument.Load(propsPath);
                    foreach (var el in xml.Descendants("PackageVersion"))
                    {
                        var name = el.Attribute("Include")?.Value;
                        var version = el.Attribute("Version")?.Value ?? el.Element("Version")?.Value;
                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(version))
                            versions[name] = version;
                    }
                    break;
                }
                catch { }
            }
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return versions;
    }

    private static string ReadTargetFramework(string csprojPath)
    {
        try
        {
            var xml = XDocument.Load(csprojPath);
            return xml.Descendants("TargetFramework").FirstOrDefault()?.Value
                ?? xml.Descendants("TargetFrameworks").FirstOrDefault()?.Value
                ?? "";
        }
        catch { return ""; }
    }

    // ── Directory tree ────────────────────────────────────────────────────

    private static readonly HashSet<string> _skipDirs =
        new(StringComparer.OrdinalIgnoreCase)
        { "bin", "obj", ".git", ".vs", ".idea", "node_modules" };

    private static void AddDirectoryContents(SolutionNode parent, string dir)
    {
        // Sub-folders first (alphabetical) — skip project sub-folders
        foreach (var sub in Directory.GetDirectories(dir).OrderBy(d => Path.GetFileName(d)))
        {
            var subName = Path.GetFileName(sub);
            if (_skipDirs.Contains(subName)) continue;

            if (Directory.GetFiles(sub, "*.*proj", SearchOption.TopDirectoryOnly).Length > 0)
                continue;

            var folderNode = new SolutionNode { Kind = SolutionNodeKind.Folder, Name = subName };
            AddDirectoryContents(folderNode, sub);
            parent.Children.Add(folderNode);
        }

        // Files — nest code-behind and designer files
        var allFiles = Directory.GetFiles(dir).OrderBy(Path.GetFileName).ToList();

        // Pass 1: Build lookup maps for potential parents (XAML and main Source files)
        var xamlNodes = new Dictionary<string, SolutionNode>(StringComparer.OrdinalIgnoreCase);
        var sourceNodes = new Dictionary<string, SolutionNode>(StringComparer.OrdinalIgnoreCase);
        var nodesToAppend = new List<SolutionNode>();

        foreach (var file in allFiles)
        {
            var name = Path.GetFileName(file);
            var ext = Path.GetExtension(file).ToLowerInvariant();
            
            var node = new SolutionNode { Kind = SolutionNodeKind.File, Name = name, FilePath = file };
            
            if (ext is ".axaml" or ".xaml")
            {
                xamlNodes[name] = node;
                nodesToAppend.Add(node);
            }
            else if (ext is ".cs" or ".fs" or ".vb")
            {
                // Only consider it a potential parent if it's not a designer file
                if (!name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith(".Designer.vb", StringComparison.OrdinalIgnoreCase))
                {
                    sourceNodes[name] = node;
                }
            }
        }

        // Pass 2: Process all other files and handle nesting
        foreach (var file in allFiles)
        {
            var name = Path.GetFileName(file);
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".axaml" or ".xaml") continue;

            SolutionNode? parentNode = null;

            // Rule 1: Avalonia code-behind (e.g., MainWindow.axaml.cs -> MainWindow.axaml)
            if (ext is ".cs" or ".fs" or ".vb")
            {
                var withoutExt = name[..^ext.Length];
                var innerExt = Path.GetExtension(withoutExt).ToLowerInvariant();
                if (innerExt is ".axaml" or ".xaml")
                {
                    xamlNodes.TryGetValue(withoutExt, out parentNode);
                }
            }

            // Rule 2: WinForms Designer (e.g., Form1.Designer.cs -> Form1.cs)
            if (parentNode == null && (name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) || 
                                     name.EndsWith(".Designer.vb", StringComparison.OrdinalIgnoreCase)))
            {
                var mainName = name.Replace(".Designer.cs", ".cs").Replace(".Designer.vb", ".vb");
                sourceNodes.TryGetValue(mainName, out parentNode);
            }

            // Rule 3: Resources (e.g., Form1.resx -> Form1.cs)
            if (parentNode == null && ext == ".resx")
            {
                var mainName = Path.GetFileNameWithoutExtension(name) + ".cs"; // Try .cs
                if (!sourceNodes.TryGetValue(mainName, out parentNode))
                {
                    mainName = Path.GetFileNameWithoutExtension(name) + ".vb"; // Try .vb
                    sourceNodes.TryGetValue(mainName, out parentNode);
                }
            }

            // Create node or use existing from sourceNodes
            SolutionNode node;
            if (sourceNodes.TryGetValue(name, out var existingSourceNode))
            {
                node = existingSourceNode;
            }
            else
            {
                node = new SolutionNode { Kind = SolutionNodeKind.File, Name = name, FilePath = file };
            }

            if (parentNode != null && parentNode != node)
            {
                node.Parent = parentNode;
                parentNode.Children.Add(node);
            }
            else if (!nodesToAppend.Contains(node))
            {
                nodesToAppend.Add(node);
            }
        }

        // Final step: Add all top-level nodes to parent
        foreach (var node in nodesToAppend)
        {
            node.Parent = parent;
            parent.Children.Add(node);
        }
    }

    // ── Folder fallback ───────────────────────────────────────────────────

    public static SolutionNode LoadFolder(string folderPath)
    {
        var root = new SolutionNode
        {
            Kind = SolutionNodeKind.Solution,
            Name = Path.GetFileName(folderPath)
        };
        AddDirectoryContents(root, folderPath);
        return root;
    }


    // ── Namespace helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Find the root namespace for a given directory by locating the nearest
    /// .csproj and reading its RootNamespace (or falling back to the project name).
    /// </summary>
    public static string GetNamespace(string directory)
    {
        try
        {
            // Walk up to find the nearest .csproj
            var dir = directory;
            while (!string.IsNullOrEmpty(dir))
            {
                var csproj = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly)
                                      .FirstOrDefault();
                if (csproj != null)
                {
                    // Try to read RootNamespace from csproj
                    var xml = XDocument.Load(csproj);
                    var ns = xml.Descendants("RootNamespace").FirstOrDefault()?.Value;
                    if (!string.IsNullOrEmpty(ns)) return ns;

                    // Fall back to project file name
                    return Path.GetFileNameWithoutExtension(csproj);
                }
                dir = Path.GetDirectoryName(dir);
            }
        }
        catch { }

        // Last resort: use the directory name
        return Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar)) ?? "MyApp";
    }

    // ── Assembly path extraction ──────────────────────────────────────────────

    /// <summary>
    /// Recursively extract assembly paths from all projects in the solution tree.
    /// Returns paths to compiled DLLs in bin/Debug or bin/Release folders.
    /// </summary>
    public static List<string> GetProjectAssemblyPaths(SolutionNode root, string configuration = "Debug")
    {
        var assemblyPaths = new List<string>();
        CollectAssemblyPaths(root, configuration, assemblyPaths);
        return assemblyPaths;
    }

    private static void CollectAssemblyPaths(SolutionNode node, string configuration, List<string> assemblyPaths)
    {
        // If this is a project node with a valid .csproj path
        if (node.Kind == SolutionNodeKind.Project && !string.IsNullOrEmpty(node.FilePath))
        {
            var csprojPath = node.FilePath;
            if (File.Exists(csprojPath))
            {
                try
                {
                    var projectDir = Path.GetDirectoryName(csprojPath)!;
                    var projectName = Path.GetFileNameWithoutExtension(csprojPath);
                    
                    // Read target framework from csproj
                    var tfm = ReadTargetFramework(csprojPath);
                    
                    // Try to find the compiled assembly
                    // Common patterns: bin/Debug/net10.0/ProjectName.dll or bin/Debug/ProjectName.dll
                    var possiblePaths = new List<string>();
                    
                    if (!string.IsNullOrEmpty(tfm))
                    {
                        // With target framework
                        possiblePaths.Add(Path.Combine(projectDir, "bin", configuration, tfm, $"{projectName}.dll"));
                        possiblePaths.Add(Path.Combine(projectDir, "bin", configuration, tfm, $"{projectName}.exe"));
                    }
                    
                    // Without target framework (fallback)
                    possiblePaths.Add(Path.Combine(projectDir, "bin", configuration, $"{projectName}.dll"));
                    possiblePaths.Add(Path.Combine(projectDir, "bin", configuration, $"{projectName}.exe"));
                    
                    // Try Release if Debug doesn't exist
                    if (configuration == "Debug")
                    {
                        if (!string.IsNullOrEmpty(tfm))
                        {
                            possiblePaths.Add(Path.Combine(projectDir, "bin", "Release", tfm, $"{projectName}.dll"));
                            possiblePaths.Add(Path.Combine(projectDir, "bin", "Release", tfm, $"{projectName}.exe"));
                        }
                        possiblePaths.Add(Path.Combine(projectDir, "bin", "Release", $"{projectName}.dll"));
                        possiblePaths.Add(Path.Combine(projectDir, "bin", "Release", $"{projectName}.exe"));
                    }
                    
                    // Add the first existing path
                    foreach (var path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            assemblyPaths.Add(path);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Error reading project {node.FilePath}: {ex.Message}");
                }
            }
        }
        
        // Recursively process children
        foreach (var child in node.Children)
        {
            CollectAssemblyPaths(child, configuration, assemblyPaths);
        }
    }

    /// <summary>
    /// Gets the directory of the project (.csproj) that contains the specified file path.
    /// Searches upward from the file path to find the nearest .csproj file.
    /// </summary>
    /// <param name="filePath">The full path to the file (e.g., AXAML file)</param>
    /// <returns>The project directory path, or null if not found</returns>
    public static string? GetProjectDirectoryFromFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var directory = Path.GetDirectoryName(filePath);

            while (!string.IsNullOrEmpty(directory))
            {
                var csprojFiles = Directory.GetFiles(directory, "*.csproj");
                if (csprojFiles.Length > 0)
                    return directory;

                var parent = Directory.GetParent(directory);
                if (parent == null) break;
                directory = parent.FullName;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Error finding project directory for file {filePath}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Gets the project name (assembly name) that contains the specified file path.
    /// Searches upward from the file path to find the nearest .csproj file.
    /// </summary>
    /// <param name="filePath">The full path to the file (e.g., AXAML file)</param>
    /// <returns>The project assembly name, or null if not found</returns>
    public static string? GetProjectNameFromFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            
            // Search upward for .csproj file
            while (!string.IsNullOrEmpty(directory))
            {
                var csprojFiles = Directory.GetFiles(directory, "*.csproj");
                if (csprojFiles.Length > 0)
                {
                    // Found a .csproj file - return its name without extension
                    return Path.GetFileNameWithoutExtension(csprojFiles[0]);
                }
                
                // Move up one directory
                var parent = Directory.GetParent(directory);
                if (parent == null)
                    break;
                    
                directory = parent.FullName;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Error finding project for file {filePath}: {ex.Message}");
        }
        
        return null;
    }
    
    // ── Solution modification ──────────────────────────────────────────────────
    
    public static void AddProjectToSolution(string solutionPath, string projectPath)
    {
        try
        {
            var ext = Path.GetExtension(solutionPath).ToLowerInvariant();
            if (ext == ".slnx")
            {
                var xml = XDocument.Load(solutionPath);
                var slnDir = Path.GetDirectoryName(solutionPath);
                var relPath = Path.GetRelativePath(slnDir!, projectPath).Replace('/', '\\');
                
                // Avoid duplicates
                if (!xml.Descendants("Project").Any(p => p.Attribute("Path")?.Value == relPath))
                {
                    xml.Root?.Add(new XElement("Project", new XAttribute("Path", relPath)));
                    xml.Save(solutionPath);
                }
            }
            else if (ext == ".sln")
            {
                var name = Path.GetFileNameWithoutExtension(projectPath);
                var slnDir = Path.GetDirectoryName(solutionPath);
                var relPath = Path.GetRelativePath(slnDir!, projectPath).Replace('/', '\\');
                var guid = Guid.NewGuid().ToString("B").ToUpper();
                var typeGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"; // C# Project
                
                var content = File.ReadAllText(solutionPath);
                if (!content.Contains(relPath, StringComparison.OrdinalIgnoreCase))
                {
                    var entry = $"\nProject(\"{typeGuid}\") = \"{name}\", \"{relPath}\", \"{guid}\"\nEndProject";
                    // Find Global section to insert before it
                    var globalIdx = content.IndexOf("Global");
                    if (globalIdx > 0)
                        content = content.Insert(globalIdx, entry + "\n");
                    else
                        content += entry;
                    
                    File.WriteAllText(solutionPath, content);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Error adding project to solution: {ex.Message}");
        }
    }

    public static void RemoveProjectFromSolution(string solutionPath, string? projectPath)
    {
        if (string.IsNullOrEmpty(projectPath)) return;
        
        try
        {
            var ext = Path.GetExtension(solutionPath).ToLowerInvariant();
            if (ext == ".slnx")
            {
                var xml = XDocument.Load(solutionPath);
                var slnDir = Path.GetDirectoryName(solutionPath);
                var relPath = Path.GetRelativePath(slnDir!, projectPath).Replace('/', '\\');
                
                var el = xml.Descendants("Project").FirstOrDefault(p => p.Attribute("Path")?.Value == relPath);
                if (el != null)
                {
                    el.Remove();
                    xml.Save(solutionPath);
                }
            }
            else if (ext == ".sln")
            {
                var slnDir = Path.GetDirectoryName(solutionPath);
                var relPath = Path.GetRelativePath(slnDir!, projectPath).Replace('/', '\\');
                
                var lines = File.ReadAllLines(solutionPath).ToList();
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Contains(relPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found project entry, usually starts 1 line before (Project line) and ends with EndProject
                        int start = i;
                        while (start >= 0 && !lines[start].TrimStart().StartsWith("Project(\"")) start--;
                        int end = i;
                        while (end < lines.Count && !lines[end].TrimStart().StartsWith("EndProject")) end++;
                        
                        if (start >= 0 && end < lines.Count)
                        {
                            lines.RemoveRange(start, end - start + 1);
                            break;
                        }
                    }
                }
                File.WriteAllLines(solutionPath, lines);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Error removing project from solution: {ex.Message}");
        }
    }
    
    public static void AddProjectReference(string targetCsproj, string referenceCsproj)
    {
        try
        {
            var xml = XDocument.Load(targetCsproj);
            var projectDir = Path.GetDirectoryName(targetCsproj);
            var relPath = Path.GetRelativePath(projectDir!, referenceCsproj).Replace('/', '\\');

            // Find or create an ItemGroup for ProjectReferences
            var itemGroup = xml.Descendants("ItemGroup")
                               .FirstOrDefault(ig => ig.Elements("ProjectReference").Any())
                         ?? xml.Root?.Elements("ItemGroup").LastOrDefault()
                         ?? new XElement("ItemGroup");

            if (itemGroup.Parent == null)
                xml.Root?.Add(itemGroup);

            // Avoid duplicates by comparing absolute paths
            var isAlreadyReferenced = xml.Descendants("ProjectReference").Any(pr =>
            {
                var inc = pr.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(inc)) return false;
                try {
                    var abs = Path.GetFullPath(Path.Combine(projectDir!, inc.Replace('\\', Path.DirectorySeparatorChar)));
                    return string.Equals(abs, Path.GetFullPath(referenceCsproj), StringComparison.OrdinalIgnoreCase);
                } catch { return false; }
            });

            if (!isAlreadyReferenced)
            {
                itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", relPath)));
                xml.Save(targetCsproj);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Error adding project reference: {ex.Message}");
        }
    }

    public static void RemoveProjectReference(string targetCsproj, string referenceCsproj)
    {
        try
        {
            var xml = XDocument.Load(targetCsproj);
            var projectDir = Path.GetDirectoryName(targetCsproj);
            var relPath = Path.GetRelativePath(projectDir!, referenceCsproj).Replace('/', '\\');

            var reference = xml.Descendants("ProjectReference")
                               .FirstOrDefault(pr =>
                               {
                                   var inc = pr.Attribute("Include")?.Value;
                                   if (string.IsNullOrEmpty(inc)) return false;
                                   try {
                                       var abs = Path.GetFullPath(Path.Combine(projectDir!, inc.Replace('\\', Path.DirectorySeparatorChar)));
                                       return string.Equals(abs, Path.GetFullPath(referenceCsproj), StringComparison.OrdinalIgnoreCase);
                                   } catch { return false; }
                               });

            if (reference != null)
            {
                var parent = reference.Parent;
                reference.Remove();
                
                // If ItemGroup is now empty, remove it too
                if (parent != null && !parent.Elements().Any())
                {
                    parent.Remove();
                }
                
                xml.Save(targetCsproj);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Error removing project reference: {ex.Message}");
        }
    }

    // ── Package conflict detection ────────────────────────────────────────

    /// <summary>
    /// Walk all Project nodes in the tree, detect package version conflicts,
    /// and set HasWarning + Tooltip on affected nodes. Safe to call from background thread.
    /// </summary>
    public static void ScanPackageConflicts(SolutionNode root)
    {
        foreach (var node in CollectProjectNodes(root))
        {
            if (string.IsNullOrEmpty(node.FilePath) || !File.Exists(node.FilePath)) continue;
            try
            {
                var projDir = Path.GetDirectoryName(node.FilePath)!;
                var (packages, _) = ReadDependencies(node.FilePath, projDir);
                var (hasConflict, tooltip) = DetectPackageConflicts(node.FilePath, projDir, packages);
                if (hasConflict)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        node.HasWarning = true;
                        node.Tooltip = tooltip;
                    });
                }
            }
            catch { }
        }
    }

    private static IEnumerable<SolutionNode> CollectProjectNodes(SolutionNode node)
    {
        if (node.Kind == SolutionNodeKind.Project && !string.IsNullOrEmpty(node.FilePath))
            yield return node;
        foreach (var child in node.Children)
            foreach (var n in CollectProjectNodes(child))
                yield return n;
    }
}
