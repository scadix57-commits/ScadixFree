using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Scadix.AxamlDesigner.PropertyGrid;
using Scadix.AxamlDesigner.Services;
using Scadix.AxamlDom;
using Scadix.Designer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scadix.Designer
{
    public class MainWindowViewModel : ObservableObject
    {
        public event Action<string>? ProjectLoaded;
        public event Action<string>? ProjectOpened;
        public event Action? ProjectClosed;

        public MainWindowViewModel()
        {
            Documents = new ObservableCollection<Document>();
            RecentFiles = new ObservableCollection<string>();
            Views = new Dictionary<object, Control>();
            SolutionTree = new ObservableCollection<SolutionNode>();
            RecentProjects = new ObservableCollection<RecentProjectEntry>();
            StartupProjects = new ObservableCollection<SolutionNode>();

            BuildCommand = new RelayCommand(BuildSolution);
            RebuildCommand = new RelayCommand(RebuildSolution);
            CleanCommand = new RelayCommand(CleanSolution);
            RunCommand = new RelayCommand(RunProject);
            
            RemoveProjectCommand = new RelayCommand<SolutionNode>(DeleteNode);
            OpenProjectFolderCommand = new RelayCommand<SolutionNode>(OpenProjectFolder);
            CopyProjectPathCommand = new RelayCommand<SolutionNode>(CopyProjectPath);
            AddExistingProjectCommand = new RelayCommand(AddExistingProject);

            Factory = new MainDockFactory();
            Layout = Factory.CreateLayout();
            Factory.InitLayout(Layout);

            Documents.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (Document doc in e.NewItems)
                        Factory.AddDocument(doc);
                if (e.OldItems != null)
                    foreach (Document doc in e.OldItems)
                        Factory.RemoveDocument(doc);
            };

            SolutionTree.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasProject));

            LoadSettings();
        }


        public static MainWindowViewModel Instance = new MainWindowViewModel();
        public const string ApplicationTitle = "Xaml Designer";

        public IPropertyGrid? PropertyGrid { get; internal set; }
        public MainDockFactory Factory { get; }
        public IRootDock Layout { get; }

        public Scadix.Designer.ViewModels.Tools.DebugToolbarViewModel DebugToolbar => Scadix.Designer.ViewModels.Tools.DebugToolbarViewModel.Instance;



        public ObservableCollection<Document> Documents { get; private set; }
        public ObservableCollection<string> RecentFiles { get; private set; }
        public ObservableCollection<SolutionNode> SolutionTree { get; private set; }
        public bool HasProject => SolutionTree.Count > 0;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }
        public ObservableCollection<RecentProjectEntry> RecentProjects { get; private set; }
        public Dictionary<object, Control> Views { get; private set; }

        public IRelayCommand BuildCommand { get; }
        public IRelayCommand RebuildCommand { get; }
        public IRelayCommand CleanCommand { get; }
        public IRelayCommand RunCommand { get; }

        public IRelayCommand<SolutionNode> RemoveProjectCommand { get; }
        public IRelayCommand<SolutionNode> OpenProjectFolderCommand { get; }
        public IRelayCommand<SolutionNode> CopyProjectPathCommand { get; }
        public IRelayCommand AddExistingProjectCommand { get; }

        public ObservableCollection<SolutionNode> StartupProjects { get; }
        
        private SolutionNode? _selectedSolutionNode;
        public SolutionNode? SelectedSolutionNode
        {
            get => _selectedSolutionNode;
            set => SetProperty(ref _selectedSolutionNode, value);
        }

        private SolutionNode? selectedStartupProject;
        public SolutionNode? SelectedStartupProject
        {
            get => selectedStartupProject;
            set
            {
                if (SetProperty(ref selectedStartupProject, value))
                {
                    // Reset all project nodes in the entire tree, then mark the selected one
                    ResetAllStartupFlags();
                    if (value != null)
                        value.IsStartup = true;
                    // Update startup projects
                    if (value != null && value.FilePath != null)
                    {
                        var projectPath = Path.GetDirectoryName(SelectedStartupProject.FilePath);
                        var projectName = Path.GetFileNameWithoutExtension(SelectedStartupProject.FilePath);
                        Settings.Default.ProjectPath = projectPath;
                        Settings.Default.ProjectName = projectName;
                        Settings.Default.Save();
                        DesignerProjectContext.ProjectRootPath = projectPath;
                        DesignerProjectContext.CurrentProjectName = projectName;
                        DesignerProjectContext.CurrentProjectPath = SelectedStartupProject.FilePath;
                    }

                    UpdateRuntimeStatus();
                }
            }
        }

        private string? _missingRuntimeMessage;
        public string? MissingRuntimeMessage
        {
            get => _missingRuntimeMessage;
            set => SetProperty(ref _missingRuntimeMessage, value);
        }

        private void UpdateRuntimeStatus()
        {
            if (SelectedStartupProject == null || string.IsNullOrEmpty(SelectedStartupProject.TargetFramework))
            {
                MissingRuntimeMessage = null;
                return;
            }

            var tfm = SelectedStartupProject.TargetFramework;
            var service = Scadix.Designer.Services.DotNetRuntimeService.Instance;
            service.EnsureInitialized();

            if (!service.IsRuntimeInstalled(tfm))
            {
                MissingRuntimeMessage = $".NET {tfm.Replace("net", "")} runtime is missing.";
            }
            else
            {
                MissingRuntimeMessage = null;
            }
        }

        /// <summary>Walk the full solution tree and clear IsStartup on every project node.</summary>
        private void ResetAllStartupFlags()
        {
            foreach (var root in SolutionTree)
                ClearStartupFlag(root);
        }

        private static void ClearStartupFlag(SolutionNode node)
        {
            if (node.Kind == SolutionNodeKind.Project)
                node.IsStartup = false;
            foreach (var child in node.Children)
                ClearStartupFlag(child);
        }

        public string[] Configurations { get; } = { "Debug", "Release" };

        private string configuration = "Debug";
        public string Configuration
        {
            get => configuration;
            set => SetProperty(ref configuration, value);
        }

        Document? currentDocument;

        public Document? CurrentDocument
        {
            get
            {
                return currentDocument;
            }
            set
            {
                if (currentDocument != value)
                {
                    currentDocument = value;
                    OnPropertyChanged("CurrentDocument");
                    OnPropertyChanged("Title");

                    if (value != null && Factory.DocumentDock != null)
                    {
                        var vm = Factory.DocumentDock.VisibleDockables?
                            .OfType<Scadix.Designer.ViewModels.DocumentViewModel>()
                            .FirstOrDefault(d => d.Document == value);

                        if (vm != null && Factory.DocumentDock.ActiveDockable != vm)
                        {
                            Factory.SetActiveDockable(vm);
                            Factory.SetFocusedDockable(Factory.DocumentDock, vm);
                        }
                    }
                }
            }
        }

        public string Title
        {
            get
            {
                if (CurrentDocument != null)
                {
                    return CurrentDocument.Title + " - " + ApplicationTitle;
                }
                return ApplicationTitle;
            }
        }

        private string _currentGitBranch = "master";
        public string CurrentGitBranch
        {
            get => _currentGitBranch;
            set => SetProperty(ref _currentGitBranch, value);
        }

        void LoadSettings()
        {
            if (Settings.Default.RecentFiles != null)
            {
                RecentFiles.AddRange(Settings.Default.RecentFiles.Cast<string>());
            }
        }

        public void SaveSettings()
        {
            /*if (Settings.Default.RecentFiles == null) {
				Settings.Default.RecentFiles = new StringCollection();
			}
			else {
				Settings.Default.RecentFiles.Clear();
			}
			foreach (var f in RecentFiles) {
				Settings.Default.RecentFiles.Add(f);
			}*/
        }

        public static void ReportException(Exception? x)
        {
            if (x != null)
            {
                // In a real app, we would use a proper dialog. For now, we trace.
                System.Diagnostics.Trace.WriteLine(x.ToString());
            }
        }

        public void JumpToError(XamlError error)
        {
            if (CurrentDocument != null && Views.ContainsKey(CurrentDocument))
            {
                (Views[CurrentDocument] as DocumentView)?.JumpToError(error);
            }
        }

        public bool CanRefresh()
        {
            return CurrentDocument != null;
        }

        public void Refresh()
        {
            CurrentDocument?.Refresh();
        }

        #region Files

        bool IsSomethingDirty
        {
            get
            {
                foreach (var doc in MainWindowViewModel.Instance.Documents)
                {
                    if (doc.IsDirty) return true;
                }
                return false;
            }
        }

        static int nonameIndex = 1;

        public void New()
        {
            Document doc = new Document("New" + nonameIndex++, File.ReadAllText("NewFileTemplate.xaml"));
            Documents.Add(doc);
            CurrentDocument = doc;
        }

        public async void Open()
        {
            var path = await MainWindow.Instance!.AskOpenFileName();
            if (path != null)
            {
                Open(path);
            }
        }

        public void Open(string path)
        {
            path = Path.GetFullPath(path);

            if (RecentFiles.Contains(path))
            {
                RecentFiles.Remove(path);
            }
            RecentFiles.Insert(0, path);

            // Check if there is already an open document for this file
            foreach (var doc in Documents)
            {
                if (doc.FilePath == path)
                {
                    CurrentDocument = doc;
                    return;
                }
            }
          
            // Fallback: Open with default XAML/Text Editor Document
            var newDoc = new Document(path);
            Documents.Add(newDoc);
            CurrentDocument = newDoc;
        }

        // ── Solution Explorer ────────────────────────────────────────────

        /// <summary>Open a .sln / .slnx / .csproj and populate SolutionTree.</summary>
        public async void OpenSolution(string path, bool append = false)
        {
            path = Path.GetFullPath(path);
            IsLoading = true;
            if (!append) SolutionTree.Clear();
            var root = await Task.Run(() => SolutionService.Load(path));

            SolutionTree.Add(root);
            IsLoading = false;

            AddToRecentProjects(path);
            UpdateStartupProjects();

            // Load breakpoints
            Scadix.Designer.Services.BreakpointService.Instance.Load(path);

            // Load project assemblies into Toolbox (background)
            _ = Task.Run(() => LoadProjectAssemblies(root));

            // Scan package conflicts in background and mark affected projects
            _ = Task.Run(() => SolutionService.ScanPackageConflicts(root));

            
            Factory.FileExplorer.LoadProject(Path.GetDirectoryName(path));
        }

        public void DeleteNode(SolutionNode? node)
        {
            if (node == null) return;

            // 1. Remove from StartupProjects
            var startup = StartupProjects.FirstOrDefault(p => p.FilePath == node.FilePath);
            if (startup != null) StartupProjects.Remove(startup);

            // 2. Physical / Logical Deletion
            if (node.Kind == SolutionNodeKind.Project)
            {
                if (SolutionTree.Count > 0 && !string.IsNullOrEmpty(SolutionTree[0].FilePath))
                {
                    SolutionService.RemoveProjectFromSolution(SolutionTree[0].FilePath, node.FilePath);
                }
            }
            else if (node.Kind == SolutionNodeKind.ProjectRef)
            {
                var owner = node.Parent;
                while (owner != null && owner.Kind != SolutionNodeKind.Project) owner = owner.Parent;

                if (owner != null && !string.IsNullOrEmpty(owner.FilePath) && !string.IsNullOrEmpty(node.FilePath))
                {
                    SolutionService.RemoveProjectReference(owner.FilePath, node.FilePath);
                }
            }
            else if ((node.Kind == SolutionNodeKind.File || node.Kind == SolutionNodeKind.Folder) && !string.IsNullOrEmpty(node.FilePath))
            {
                try
                {
                    if (node.Kind == SolutionNodeKind.File && File.Exists(node.FilePath))
                        File.Delete(node.FilePath);
                    else if (node.Kind == SolutionNodeKind.Folder && Directory.Exists(node.FilePath))
                        Directory.Delete(node.FilePath, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Error deleting disk item: {ex.Message}");
                }
            }

            // 3. Remove from tree
            RemoveNodeFromTree(SolutionTree, node);
        }

        private bool RemoveNodeFromTree(ObservableCollection<SolutionNode> collection, SolutionNode target)
        {
            if (collection.Remove(target)) return true;
            foreach (var node in collection)
            {
                if (RemoveNodeFromTree(node.Children, target)) return true;
            }
            return false;
        }

        public void OpenProjectFolder(SolutionNode? node)
        {
            if (node == null || string.IsNullOrEmpty(node.FilePath)) return;
            var path = node.Kind == SolutionNodeKind.Folder ? node.FilePath : Path.GetDirectoryName(node.FilePath);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        }

        public void CopyProjectPath(SolutionNode? node)
        {
            if (node == null || string.IsNullOrEmpty(node.FilePath)) return;
            MainWindow.Instance?.Clipboard?.SetTextAsync(node.FilePath);
        }

        public async void AddExistingProject()
        {
            if (SolutionTree.Count == 0) return;

            var path = await MainWindow.Instance!.AskOpenSolutionFileName();
            if (path != null)
            {
                var sln = SolutionTree[0];
                var name = Path.GetFileNameWithoutExtension(path);
                var proj = SolutionService.BuildCsprojNode(name, path);
                proj.Parent = sln;
                sln.Children.Add(proj);
                UpdateStartupProjects();

                // Update solution file
                if (!string.IsNullOrEmpty(sln.FilePath))
                {
                    SolutionService.AddProjectToSolution(sln.FilePath, path);
                }
            }
        }

        public void OpenFolder(string folderPath, bool append = false)
        {
            if (!append) SolutionTree.Clear();
            var root = SolutionService.LoadFolder(folderPath);
            SolutionTree.Add(root);
            AddToRecentProjects(folderPath);
            UpdateStartupProjects();

            // Load breakpoints
            Scadix.Designer.Services.BreakpointService.Instance.Load(folderPath);

            // Load project assemblies into Toolbox
            LoadProjectAssemblies(root);

          

            Factory.FileExplorer.LoadProject(folderPath);
        }

      

        /// <summary>Called when the user double-clicks a file node in Solution Explorer.</summary>
        public void OpenSolutionFile(SolutionNode node)
        {
            if (node.FilePath == null) return;
            var ext = Path.GetExtension(node.FilePath).ToLowerInvariant();

            // XAML files → open in designer
            if (ext is ".xaml" or ".axaml")
            {
                Open(node.FilePath);
                return;
            }

            // Solution / project files → load as solution
            if (ext is ".sln" or ".slnx" or ".csproj")
            {
                OpenSolution(node.FilePath);
                return;
            }
        }
        public void OpenFile(string path)
        {
            Open(path);
        }
        public async void OpenSolutionDialog()
        {
            var path = await MainWindow.Instance!.AskOpenSolutionFileName();
            if (path != null)
                OpenSolution(path);
        }

        public async void OpenFolderDialog()
        {
            var path = await MainWindow.Instance!.AskOpenFolderName();
            if (path != null)
                OpenFolder(path);
        }

        /// <summary>Reload the current solution from disk.</summary>
        public void RefreshSolution()
        {
            if (SolutionTree.Count == 0) return;
            var path = SolutionTree[0].FilePath;
            if (path != null) OpenSolution(path);

            // Update startup projects
            UpdateStartupProjects();
        }

        /// <summary>
        /// Traverse all Project nodes in the solution tree and invoke
        /// <see cref="ProjectReferencesLoader"/> for each project folder.
        /// </summary>
        private void LoadProjectAssemblies(SolutionNode root)
        {
            var loader = new ProjectReferencesLoader();
            TraverseAndLoad(root, loader);
        }

        private static void TraverseAndLoad(SolutionNode node, ProjectReferencesLoader loader)
        {
            if (node.Kind == SolutionNodeKind.Project && !string.IsNullOrEmpty(node.FilePath))
            {
                var projectFolder = Path.GetDirectoryName(node.FilePath);
                if (!string.IsNullOrEmpty(projectFolder) && Directory.Exists(projectFolder))
                    loader.LoadAllReferences(projectFolder);
            }

            foreach (var child in node.Children)
                TraverseAndLoad(child, loader);
        }

        private void UpdateStartupProjects()
        {
            StartupProjects.Clear();
            foreach (var root in SolutionTree)
            {
                FindProjects(root);
            }

            if (SelectedStartupProject == null && StartupProjects.Count > 0)
            {
                SelectedStartupProject = StartupProjects[0];
                var projectPath = Path.GetDirectoryName(SelectedStartupProject.FilePath);
                var projectName = Path.GetFileNameWithoutExtension(SelectedStartupProject.FilePath);
                Settings.Default.ProjectPath = projectPath;
                Settings.Default.ProjectName = projectName;
                Settings.Default.Save();
                DesignerProjectContext.ProjectRootPath = projectPath;
                DesignerProjectContext.CurrentProjectName = projectName;
                DesignerProjectContext.CurrentProjectPath = SelectedStartupProject.FilePath;
            }
        }

        private void FindProjects(SolutionNode node)
        {
            if (node.Kind == SolutionNodeKind.Project)
            {
                if (!string.IsNullOrEmpty(node.Badge) && node.Badge.Contains(';'))
                {
                    var tfms = node.Badge.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tfm in tfms)
                    {
                        var trimmedTfm = tfm.Trim();
                        StartupProjects.Add(new SolutionNode
                        {
                            Kind = SolutionNodeKind.Project,
                            Name = node.Name,
                            Badge = trimmedTfm,
                            TargetFramework = trimmedTfm,
                            FilePath = node.FilePath
                        });
                    }
                }
                else
                {
                    node.TargetFramework = node.Badge;
                    StartupProjects.Add(node);
                }
            }

            foreach (var child in node.Children)
                FindProjects(child);
        }

        /// <summary>Add or move a path to the top of RecentProjects.</summary>
        private void AddToRecentProjects(string path)
        {
            // Remove existing entry for same path
            var existing = RecentProjects.FirstOrDefault(
                r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                RecentProjects.Remove(existing);

            // Insert at top
            var entry = RecentProjectEntry.FromPath(path);
            RecentProjects.Insert(0, entry);

            // Keep max 20 entries
            while (RecentProjects.Count > 20)
                RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }

        public bool Save(Document doc)
        {
            if (doc.IsDirty)
            {
                if (doc.FilePath == null)
                {
                    SaveAs(doc);
                    return true; // async save handled separately
                }
                doc.Save();
            }
            return true;
        }

        public async void SaveAs(Document doc)
        {
            var initName = doc.FileName ?? doc.Name + ".xaml";
            var path = await MainWindow.Instance!.AskSaveFileName(initName);
            if (path != null)
            {
                doc.SaveAs(path);
            }
        }

        public bool SaveAll()
        {
            foreach (var doc in Documents)
            {
                if (!Save(doc)) return false;
            }
            return true;
        }

        public bool Close(Document doc)
        {
            Documents.Remove(doc);
            Views.Remove(doc);

            // Also remove from Dock layout
            if (Factory.DocumentDock?.VisibleDockables != null)
            {
                var vm = Factory.DocumentDock.VisibleDockables
                    .OfType<Scadix.Designer.ViewModels.DocumentViewModel>()
                    .FirstOrDefault(v => v.Document == doc);
                if (vm != null)
                    Factory.DocumentDock.VisibleDockables.Remove(vm);
            }

            return true;
        }

        /// <summary>
        /// إغلاق مستند مع عرض dialog التأكيد إذا كان غير محفوظ
        /// مطابق لـ MyDesigner.XamlDesigner Shell.Close
        /// </summary>
        public async System.Threading.Tasks.Task<bool> CloseAsync(Document doc)
        {
            if (doc.IsDirty)
            {
                var dialog = new Scadix.Designer.Views.SaveConfirmationDialog(
                    $"Save changes to \"{doc.Name}\"?");

                var owner = MainWindow.Instance;
                if (owner != null)
                    await dialog.ShowDialog(owner);
                else
                    dialog.Show();

                if (dialog.Result == Scadix.Designer.Views.SaveConfirmationResult.Cancel)
                    return false;

                if (dialog.Result == Scadix.Designer.Views.SaveConfirmationResult.Save)
                {
                    if (!Save(doc)) return false;
                }
                // Discard → continue closing without saving
            }

            Documents.Remove(doc);
            Views.Remove(doc);

            // Also remove from Dock layout
            if (Factory.DocumentDock?.VisibleDockables != null)
            {
                var vm = Factory.DocumentDock.VisibleDockables
                    .OfType<Scadix.Designer.ViewModels.DocumentViewModel>()
                    .FirstOrDefault(v => v.Document == doc);
                if (vm != null)
                    Factory.DocumentDock.VisibleDockables.Remove(vm);
            }

            return true;
        }

        public bool CloseAll()
        {
            foreach (var doc in Documents.ToArray())
            {
                if (!Close(doc)) return false;
            }

            // Ensure all dockables are cleared (including custom designers)
            Factory.DocumentDock?.VisibleDockables?.Clear();

            return true;
        }

        public bool PrepareExit()
        {
            if (IsSomethingDirty)
            {
                // Prompt to save all could be added here
            }
            return true;
        }

        public void Exit()
        {
            MainWindow.Instance?.Close();
        }

        public void SaveCurrentDocument()
        {
           
            
             if (CurrentDocument != null)
            {
                Save(CurrentDocument);
            }
        }

        public void SaveCurrentDocumentAs()
        {
            if (CurrentDocument != null)
                SaveAs(CurrentDocument);
        }

        public void CloseCurrentDocument()
        {
            if (CurrentDocument != null)
                Close(CurrentDocument);
        }

        /// <summary>Close the current project/solution and all related documents.</summary>
        public bool CloseProject()
        {
            // 1. Close all open documents
            if (!CloseAll())
                return false;

            // 2. Stop debugging if active
            if (DebuggerService.Instance.IsDebugging)
                DebuggerService.Instance.StopDebugging();

            // 3. Clear solution tree and startup state
            SolutionTree.Clear();
            StartupProjects.Clear();
            SelectedStartupProject = null;

            // 4. Clear project-specific tools
            Factory.Git.Clear();
            Factory.Commit.Clear();
            Factory.Errors.Clear();
            BuildOutputService.Instance.Clear();
            Factory.Locals.Clear();
            Factory.CallStack.Clear();
            Factory.Diagnostics.Clear();
            Factory.LogicalTree.Clear();
            Factory.FileExplorer.Clear();

            // 5. Clear global services
            BuildOutputService.Instance.Clear();
            BreakpointService.Instance.ClearAll();
            
            if (PropertyGrid != null)
                PropertyGrid.SelectedItems = null;

            // 6. Clear global states
            CurrentDocument = null;
            
            // Invoke the event
            ProjectClosed?.Invoke();

            DesignerProjectContext.CurrentProjectAssemblyName = "";
            DesignerProjectContext.CurrentProjectName = "";
            DesignerProjectContext.CurrentXamlFilePath = "";
            DesignerProjectContext.CurrentProjectPath = "";
            DesignerProjectContext.ProjectRootPath = "";

            return true;
        }

        #endregion

        #region Build / Run Operations

        private void BuildSolution() => _ = RunDotnetCommand($"build -c {Configuration}");
        private void RebuildSolution() => _ = RunDotnetCommand($"build -c {Configuration} --no-incremental");
        private void CleanSolution() => _ = RunDotnetCommand($"clean -c {Configuration}");
        private void RunProject()
        {
            var args = $"run -c {Configuration}";
            if (SelectedStartupProject != null && !string.IsNullOrEmpty(SelectedStartupProject.TargetFramework))
            {
                args += $" -f {SelectedStartupProject.TargetFramework}";
            }
            _ = RunDotnetCommand(args, SelectedStartupProject?.FilePath);
        }

        public async Task RunDotnetCommand(string args, string? projectPath = null)
        {

            BuildOutputService.Instance.BuildLogs.Clear();

            string? targetPath = projectPath;
            if (string.IsNullOrEmpty(targetPath))
            {
                var slnNode = SolutionTree.FirstOrDefault();
                targetPath = slnNode?.FilePath;
            }

            if (string.IsNullOrEmpty(targetPath))
            {
                BuildOutputService.Instance.AppendLine("Error: No solution or project open.");
                return;
            }

            var workingDir = Path.GetDirectoryName(targetPath);
            BuildOutputService.Instance.AppendLine($"Target: {Path.GetFileName(targetPath)}");
            BuildOutputService.Instance.AppendLine($"Working Directory: {workingDir}");

            // If we are targeting a specific project, include it in the args
            string finalArgs = args;
            if (!string.IsNullOrEmpty(projectPath))
            {
                finalArgs = $"{args} \"{projectPath}\"";
            }

            BuildOutputService.Instance.AppendLine($"> dotnet {finalArgs}");

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("dotnet", finalArgs)
                {
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new System.Diagnostics.Process { StartInfo = psi };

                if (!process.Start())
                {
                    BuildOutputService.Instance.AppendLine("Error: Failed to start 'dotnet' process.");
                    return;
                }

                // Capture output in real-time using tasks instead of events for better reliability
                var outputTask = Task.Run(async () =>
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = await process.StandardOutput.ReadLineAsync();
                        if (line != null) BuildOutputService.Instance.AppendLine(line);
                    }
                });

                var errorTask = Task.Run(async () =>
                {
                    while (!process.StandardError.EndOfStream)
                    {
                        var line = await process.StandardError.ReadLineAsync();
                        if (line != null) BuildOutputService.Instance.AppendLine("Error: " + line);
                    }
                });

                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync());
                BuildOutputService.Instance.AppendLine($"\nProcess finished with exit code {process.ExitCode}");
            }
            catch (Exception ex)
            {
                BuildOutputService.Instance.AppendLine($"Exception: {ex.Message}");
                BuildOutputService.Instance.AppendLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion
    }
}
