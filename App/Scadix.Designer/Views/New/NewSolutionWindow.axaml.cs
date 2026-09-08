using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Scadix.Designer;

public partial class NewSolutionWindow : Window
{
    private List<TemplateCategory> _categories      = new();
    private TemplateCategory?      _selectedCategory;
    private ProjectTemplate?       _selectedTemplate;
    private string                 _selectedLanguage = "C#";
    private bool                   _userEditedProjectName;

    /// <summary>Set by WelcomeScreen so we can close it after creating a project.</summary>
    public WelcomeScreen? OwnerWelcomeScreen { get; set; }

    public NewSolutionWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ── Init ─────────────────────────────────────────────────────────────────

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        LocationBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "ScadixProjects");

        // Default framework = net10.0
        FrameworkCombo.SelectedIndex = 0;
        SdkCombo.SelectedIndex       = 0;

        UpdatePathPreview();
        HighlightLanguage("C#");

        // ProjectNameBox tracks SolutionNameBox until user edits it manually
        ProjectNameBox.TextChanged += (_, _) =>
        {
            if (ProjectNameBox.IsFocused)
                _userEditedProjectName = true;
        };

        // Load templates async
        Task.Run(() => TemplateCatalog.Load())
            .ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                _categories = t.Result;
                BuildCategoryPanel(_categories);
                if (_categories.Count > 0)
                    SelectCategory(_categories[0]);
                
                CheckMissingTemplates();
            }));
    }

    private async void CheckMissingTemplates()
    {
        try
        {
            var packages = await TemplateManager.GetPackagesStatusAsync();
            if (packages.Any(p => !p.IsInstalled))
            {
                // Show warning in the PathPreview or a dedicated area
                PathPreview.Text = "⚠ Some essential templates are missing. Click 'Install Templates' below to set them up.";
                PathPreview.Foreground = Avalonia.Media.Brushes.DarkOrange;
            }
        }
        catch { }
    }

    // ── Left panel ───────────────────────────────────────────────────────────

    private void BuildCategoryPanel(IEnumerable<TemplateCategory> categories)
    {
        // Keep the first 3 children (Empty Solution button + separator + "Project Type" label)
        // and remove everything after them
        while (CategoryPanel.Children.Count > 3)
            CategoryPanel.Children.RemoveAt(CategoryPanel.Children.Count - 1);

        // "Project Type" header
        if (CategoryPanel.Children.Count < 3)
        {
            CategoryPanel.Children.Add(new TextBlock
            {
                Text       = "Project Type",
                FontSize   = 11,
                Foreground = Avalonia.Media.Brushes.Gray,
                Margin     = new Avalonia.Thickness(4, 4, 0, 4)
            });
        }

        foreach (var cat in categories)
        {
            var btn = new Button { Classes = { "cat-item" }, Tag = cat };
            btn.Content = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing     = 8,
                Children    =
                {
                    new TextBlock { Text = cat.Icon, FontSize = 14,
                                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                    new TextBlock { Text = cat.Name, FontSize = 13,
                                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }
                }
            };
            btn.Click += (_, _) => SelectCategory(cat);
            CategoryPanel.Children.Add(btn);
        }
    }

    private void SelectCategory(TemplateCategory cat)
    {
        _selectedCategory = cat;

        // Deselect all category buttons
        foreach (var child in CategoryPanel.Children.OfType<Button>())
        {
            child.Classes.Remove("selected");
            if (child.Tag == cat) child.Classes.Add("selected");
        }

        ShowTemplates(cat.Templates);
    }

    private void ShowTemplates(List<ProjectTemplate> templates)
    {
        // Remove old template sub-items
        var toRemove = CategoryPanel.Children
            .OfType<Button>()
            .Where(b => b.Tag is ProjectTemplate)
            .ToList();
        foreach (var b in toRemove) CategoryPanel.Children.Remove(b);

        // Find insertion index (after selected category button)
        int insertIdx = -1;
        for (int i = 0; i < CategoryPanel.Children.Count; i++)
        {
            if (CategoryPanel.Children[i] is Button b && b.Tag == _selectedCategory)
            { insertIdx = i + 1; break; }
        }
        if (insertIdx < 0) insertIdx = CategoryPanel.Children.Count;

        var filtered = templates
            .Where(t => string.IsNullOrEmpty(_selectedLanguage) ||
                        t.Language.Contains(_selectedLanguage, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var tpl in filtered)
        {
            var btn = new Button
            {
                Classes = { "tpl-item" },
                Tag     = tpl,
                Margin  = new Avalonia.Thickness(16, 0, 0, 0)
            };
            btn.Content = new TextBlock
            {
                Text         = tpl.Name,
                FontSize     = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            btn.Click += (_, _) => SelectTemplate(tpl);
            CategoryPanel.Children.Insert(insertIdx++, btn);
        }

        // Auto-select first template
        if (filtered.Count > 0) SelectTemplate(filtered[0]);
        else ClearTemplateSelection();
    }

    private void SelectTemplate(ProjectTemplate tpl)
    {
        var previousTemplate = _selectedTemplate;
        _selectedTemplate = tpl;

        // Update button styles
        foreach (var child in CategoryPanel.Children.OfType<Button>())
        {
            child.Classes.Remove("selected");
            if (child.Tag == tpl) child.Classes.Add("selected");
        }

        // Fill template name into Solution/Project name boxes ONLY if they are empty or still have the previous template's safe name
        var safeName = MakeSafeName(tpl.Name);
        if (string.IsNullOrEmpty(SolutionNameBox.Text) || SolutionNameBox.Text == "MySolution" || (previousTemplate != null && SolutionNameBox.Text == MakeSafeName(previousTemplate.Name)))
        {
            SolutionNameBox.Text = safeName;
        }

        if (!_userEditedProjectName && (string.IsNullOrEmpty(ProjectNameBox.Text) || ProjectNameBox.Text == "MySolution" || (previousTemplate != null && ProjectNameBox.Text == MakeSafeName(previousTemplate.Name))))
            ProjectNameBox.Text = safeName;

        SelectedTemplateName.Text = tpl.Name;
        TypeLabel.Text            = tpl.ShortName;
        TemplateDescription.Text  = string.IsNullOrEmpty(tpl.Description)
            ? $"Creates a new {tpl.Name} project using `dotnet new {tpl.ShortName}`."
            : tpl.Description;

        CreateBtn.IsEnabled = true;
        UpdatePathPreview();
    }

    private void ClearTemplateSelection()
    {
        _selectedTemplate         = null;
        SelectedTemplateName.Text = "Select a template";
        TypeLabel.Text            = "";
        TemplateDescription.Text  = "Select a template to see its description.";
        CreateBtn.IsEnabled       = false;
    }

    // ── Empty Solution ────────────────────────────────────────────────────────

    private void EmptySolution_Click(object? sender, RoutedEventArgs e)
    {
        // Deselect all
        foreach (var child in CategoryPanel.Children.OfType<Button>())
            child.Classes.Remove("selected");
        if (sender is Button btn) btn.Classes.Add("selected");

        _selectedTemplate         = new ProjectTemplate
        {
            Name      = "Empty Solution",
            ShortName = "sln",
            Category  = "Other",
            Language  = "C#"
        };
        SelectedTemplateName.Text = "Empty Solution";
        TypeLabel.Text            = "sln";
        TemplateDescription.Text  = "Creates an empty .NET solution file with no projects.";
        SolutionNameBox.Text      = "MySolution";
        if (!_userEditedProjectName) ProjectNameBox.Text = "MySolution";
        CreateBtn.IsEnabled       = true;
        UpdatePathPreview();
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(query))
        {
            BuildCategoryPanel(_categories);
            if (_selectedCategory != null) SelectCategory(_selectedCategory);
            return;
        }

        var matched = _categories
            .SelectMany(c => c.Templates)
            .Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        t.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var searchCat = new TemplateCategory
            { Name = "Search Results", Icon = "🔍", Templates = matched };
        BuildCategoryPanel(new[] { searchCat });
        SelectCategory(searchCat);
    }

    // ── Form ─────────────────────────────────────────────────────────────────

    private void SolutionName_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_userEditedProjectName)
            ProjectNameBox.Text = SolutionNameBox.Text;
        UpdatePathPreview();
    }

    private void UpdatePathPreview()
    {
        var dir  = LocationBox.Text?.Trim() ?? "";
        var name = SolutionNameBox.Text?.Trim() ?? "";
        
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name))
        {
            PathPreview.Text = "";
            FolderExistsWarning.IsVisible = false;
            return;
        }

        var fullPath = Path.Combine(dir, name);
        PathPreview.Text = $"The project will be created in {fullPath}";

        // Check if directory already exists
        if (Directory.Exists(fullPath))
        {
            FolderExistsWarning.IsVisible = true;
            // Optionally we could suggest a name here, but the user requested the warning
        }
        else
        {
            FolderExistsWarning.IsVisible = false;
        }
    }

    private void Language_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string lang) return;
        _selectedLanguage = lang;
        HighlightLanguage(lang);

        if (_selectedCategory == null) return;

        // Remember current selection before rebuilding
        var previousTemplate = _selectedTemplate;

        ShowTemplates(_selectedCategory.Templates);

        // Restore previous selection if it still exists in the filtered list
        if (previousTemplate != null)
        {
            var stillExists = _selectedCategory.Templates
                .Any(t => t.ShortName == previousTemplate.ShortName &&
                          t.Language.Contains(lang, StringComparison.OrdinalIgnoreCase));

            if (stillExists)
                SelectTemplate(previousTemplate);
        }
    }

    private void HighlightLanguage(string lang)
    {
        foreach (var btn in new[] { LangCSharp, LangVB })
        {
            if (btn == null) continue;
            btn.Classes.Remove("selected");
            if (btn.Tag?.ToString() == lang) btn.Classes.Add("selected");
        }
    }

    private async void BrowseLocation_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Select Solution Directory", AllowMultiple = false });

        if (folders.Count > 0)
        {
            LocationBox.Text = folders[0].Path.LocalPath;
            UpdatePathPreview();
        }
    }

    // ── Create ────────────────────────────────────────────────────────────────

    private async void Create_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedTemplate == null) return;

        var solutionName = SolutionNameBox.Text?.Trim() ?? "MySolution";
        var projectName  = ProjectNameBox.Text?.Trim()  ?? solutionName;
        var location     = LocationBox.Text?.Trim()     ?? "";
        var framework    = (FrameworkCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "net10.0";

        if (string.IsNullOrEmpty(location))
        {
            PathPreview.Text = "⚠ Please specify a solution directory.";
            return;
        }

        // Create directory if it doesn't exist
        var solutionDir = Path.Combine(location, solutionName);
        var outputDir   = Path.Combine(solutionDir, projectName);

        try { Directory.CreateDirectory(outputDir); }
        catch
        {
            PathPreview.Text = "⚠ Cannot create directory. Check permissions.";
            return;
        }

        CreateBtn.IsEnabled = false;
        CreateBtn.Content   = "Creating...";

        var shortName = _selectedTemplate.ShortName;
        var success   = await Task.Run(() =>
        {
            if (shortName == "sln")
                return RunDotnetNewSln(solutionName, solutionDir);
            else if (shortName.StartsWith("scadix."))
                return RunLocalTemplate(shortName, projectName, outputDir);
            else
            {
                // Uno templates use -tfm instead of --framework
                bool isUno = shortName.Contains("uno", StringComparison.OrdinalIgnoreCase);
                string fwArg = isUno ? "-tfm" : "--framework";
                return RunDotnetNew(shortName, projectName, outputDir, framework, fwArg);
            }
        });

        if (success)
        {
            // 0. Git Init if requested
            if (CreateGitRepo.IsChecked == true)
            {
                await Task.Run(() => RunGitInit(solutionDir));
            }

            // Search recursively for .sln or .slnx — some templates create them
            // in sub-folders (e.g. avalonia.xplat creates a .slnx inside a sub-dir)
            var slnPath =
                Directory.GetFiles(solutionDir, "*.sln",  SearchOption.AllDirectories).FirstOrDefault()
             ?? Directory.GetFiles(solutionDir, "*.slnx", SearchOption.AllDirectories).FirstOrDefault()
             ?? Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();

            // 1. Close this dialog
            Close();

            // 2. Open MainWindow and transfer app lifetime to it
            var main = new MainWindow();
            if (Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = main;
            }
            main.Show();

            // 3. Load solution AFTER MainWindow is shown so SolutionExplorer is ready
            if (slnPath != null)
            {
                main.Loaded += (_, _) => MainWindowViewModel.Instance.OpenSolution(slnPath);
            }

            // 4. Close WelcomeScreen
            OwnerWelcomeScreen?.Close();
        }
        else
        {
            CreateBtn.IsEnabled = true;
            CreateBtn.Content   = "Create";
            PathPreview.Text    = "⚠ Failed to create project. Ensure the .NET SDK is installed.";
        }
    }

    private static bool RunDotnetNew(string shortName, string projectName,
                                     string outputDir, string framework, string frameworkArg = "--framework")
    {
        try
        {
            var args = $"new {shortName} --name \"{projectName}\" --output \"{outputDir}\" {frameworkArg} {framework} --force";
            var psi  = new ProcessStartInfo("dotnet", args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(60_000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static bool RunLocalTemplate(string shortName, string projectName, string outputDir)
    {
        try
        {
            string templateFolder = shortName switch
            {
                "scadix.avalonia" => "Avalonia",
                "scadix.avalonia.mvvm" => "AvaloniaMVVM",
                "scadix.avalonia.xplat" => "AvaloniaXPlat",
                "scadix.wpf" => "WPF",
                "scadix.maui" => "Maui",
                "scadix.uno" => "Uno",
                _ => ""
            };

            if (string.IsNullOrEmpty(templateFolder)) return false;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string sourcePath = Path.Combine(baseDir, "Templates", templateFolder);

            if (!Directory.Exists(sourcePath))
            {
                // Fallback for debugging mode
                var current = new DirectoryInfo(baseDir);
                while (current != null)
                {
                    var testPath = Path.Combine(current.FullName, "Templates", templateFolder);
                    if (Directory.Exists(testPath))
                    {
                        sourcePath = testPath;
                        break;
                    }
                    current = current.Parent;
                }
            }

            if (!Directory.Exists(sourcePath)) return false;

            string placeholder = "TemplateProjectName";
            CopyAndReplaceDirectory(sourcePath, outputDir, placeholder, projectName);
            
            // Also create a basic solution file if needed, or assume caller will create it?
            // The template itself is just the project.
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    private static void CopyAndReplaceDirectory(string sourceDir, string targetDir, string placeholder, string replacement)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var newFileName = fileName.Replace(placeholder, replacement);
            var destFile = Path.Combine(targetDir, newFileName);

            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".png" || ext == ".jpg" || ext == ".ico" || ext == ".dll" || ext == ".exe")
            {
                File.Copy(file, destFile, true);
            }
            else
            {
                var content = File.ReadAllText(file);
                var replacedContent = content.Replace(placeholder, replacement);
                File.WriteAllText(destFile, replacedContent);
            }
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            // Skip obj/bin just in case
            if (dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) || 
                dirName.Equals("obj", StringComparison.OrdinalIgnoreCase)) continue;

            var newDirName = dirName.Replace(placeholder, replacement);
            var destDir = Path.Combine(targetDir, newDirName);
            CopyAndReplaceDirectory(dir, destDir, placeholder, replacement);
        }
    }

    private static bool RunDotnetNewSln(string solutionName, string outputDir)
    {
        try
        {
            var args = $"new sln --name \"{solutionName}\" --output \"{outputDir}\" --force";
            var psi  = new ProcessStartInfo("dotnet", args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(30_000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static void RunGitInit(string workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "init")
            {
                WorkingDirectory = workingDir,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(10_000);
            
            // Also create a default .gitignore if none exists
            var ignorePath = Path.Combine(workingDir, ".gitignore");
            if (!File.Exists(ignorePath))
            {
                File.WriteAllText(ignorePath, "bin/\nobj/\n.vs/\n.idea/\n*.user\n");
            }

            // Stage and commit initial files so 'master' branch is created
            RunGit(workingDir, "add .");
            RunGit(workingDir, "commit -m \"Initial commit\"");
        }
        catch { }
    }

    private static void RunGit(string workingDir, string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = workingDir,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(10_000);
        }
        catch { }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();

    private async void InstallTemplates_Click(object? sender, RoutedEventArgs e)
    {
        var win = new TemplateInstallerWindow();
        await win.ShowDialog(this);

        // Refresh catalog after closing the installer
        Task.Run(() => TemplateCatalog.Load(forceRefresh: true))
            .ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                _categories = t.Result;
                BuildCategoryPanel(_categories);
                if (_categories.Count > 0) SelectCategory(_categories[0]);
                
                // Clear warning if fixed
                PathPreview.Text = "";
                UpdatePathPreview();
            }));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MakeSafeName(string templateName)
    {
        // "Avalonia MVVM Application" → "AvaloniaApp"
        var words = templateName
            .Replace("Application", "App")
            .Replace("Library", "Lib")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1 && !new[] { "a", "an", "the", "for", "and", "or", "with" }
                .Contains(w.ToLowerInvariant()))
            .Take(3);
        return string.Concat(words.Select(w => char.ToUpper(w[0]) + w[1..]));
    }
}
