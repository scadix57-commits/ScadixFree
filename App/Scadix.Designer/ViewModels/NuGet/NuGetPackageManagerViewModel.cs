using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scadix.Designer.Models.NuGet;
using Scadix.Designer.Services.NuGet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Scadix.Designer.ViewModels.NuGet
{
    public partial class NuGetPackageManagerViewModel : ObservableObject
    {
        private readonly NuGetService _nugetService;
        private readonly PackageSourcesService _packageSourcesService;
        private readonly string _projectPath;

        [ObservableProperty]
        private string _projectName;

        [ObservableProperty]
        private string _searchQuery = "";

        [ObservableProperty]
        private ObservableCollection<NuGetPackageInfo> _packages = new();

        [ObservableProperty]
        private NuGetPackageInfo? _selectedPackage;

        [ObservableProperty]
        private string? _selectedVersion;

        [ObservableProperty]
        private bool _isBrowseTabSelected = true;

        [ObservableProperty]
        private bool _isInstalledTabSelected;

        [ObservableProperty]
        private bool _isUpdatesTabSelected;

        [ObservableProperty]
        private bool _isSettingsTabSelected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        private bool _isLoading;

        /// <summary>Text for the single action button in the projects grid.</summary>
        [ObservableProperty]
        private string _projectsActionButtonText = "Install into selected projects";

        /// <summary>Style class for the single action button (PrimaryButton or UninstallButton).</summary>
        [ObservableProperty]
        private string _projectsActionButtonClass = "PrimaryButton";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        private bool _isInstalling;

        public bool IsBusy => IsLoading || IsInstalling;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private string _loadingMessage = "";

        [ObservableProperty]
        private string _installButtonText = "Install";

        // Search Options
        [ObservableProperty]
        private bool _includePrerelease = false;

        [ObservableProperty]
        private int _resultsPerPage = 50; // عدد النتائج في الصفحة

        public int ResultsPerPageIndex
        {
            get => ResultsPerPage switch
            {
                20 => 0,
                50 => 1,
                100 => 2,
                200 => 3,
                1000 => 4,
                _ => 1
            };
            set
            {
                ResultsPerPage = value switch
                {
                    0 => 20,
                    1 => 50,
                    2 => 100,
                    3 => 200,
                    4 => 1000,
                    _ => 50
                };
                OnPropertyChanged(nameof(ResultsPerPageIndex));
            }
        }

        [ObservableProperty]
        private PackageSourceInfo? _selectedSearchSource; // المصدر المختار للبحث

        [ObservableProperty]
        private bool _isNoProjectMode = true; // true إذا لم يكن هناك مشروع محمل

        // Settings Tab Properties
        [ObservableProperty]
        private ObservableCollection<PackageSourceInfo> _packageSources = new();

        [ObservableProperty]
        private PackageSourceInfo? _selectedPackageSource;

        [ObservableProperty]
        private bool _isAddingSource;

        [ObservableProperty]
        private string _newSourceName = "";

        [ObservableProperty]
        private string _newSourcePath = "";

        [ObservableProperty]
        private int _newSourceTypeIndex = 0;

        [ObservableProperty]
        private bool _isLocalSourceType = true;

        // ── Project Install Grid ──────────────────────────────────────────────

        /// <summary>All loaded projects shown in the install grid.</summary>
        [ObservableProperty]
        private ObservableCollection<ProjectInstallEntry> _projectEntries = new();

        /// <summary>Install/Update/Uninstall into checked projects (legacy single-action).</summary>
        public ICommand InstallIntoProjectsCommand { get; }

        /// <summary>Install selected version into checked projects.</summary>
        public ICommand InstallCheckedCommand { get; }

        /// <summary>Update to selected version in checked projects.</summary>
        public ICommand UpdateCheckedCommand { get; }

        /// <summary>Uninstall from checked projects.</summary>
        public ICommand UninstallCheckedCommand { get; }

        public ICommand SearchCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand UninstallCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand UpdateAllCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenProjectUrlCommand { get; }

        // Settings Commands
        public ICommand AddPackageSourceCommand { get; }
        public ICommand EditPackageSourceCommand { get; }
        public ICommand RemovePackageSourceCommand { get; }
        public ICommand SavePackageSourceCommand { get; }
        public ICommand CancelAddSourceCommand { get; }
        public ICommand BrowseSourcePathCommand { get; }

        public NuGetPackageManagerViewModel()
        {
            _nugetService = new NuGetService();
            _packageSourcesService = new PackageSourcesService();
            _projectPath = "";
            _projectName = "No Project (Browse Only)"; // يعمل بدون مشروع
            IsNoProjectMode = true; // لا يوجد مشروع

            SearchCommand = new AsyncRelayCommand(SearchPackagesAsync);
            InstallCommand = new AsyncRelayCommand(InstallPackageAsync);
            UninstallCommand = new AsyncRelayCommand(UninstallPackageAsync);
            UpdateCommand = new AsyncRelayCommand(UpdatePackageAsync);
            UpdateAllCommand = new AsyncRelayCommand(UpdateAllPackagesAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshCurrentTabAsync);
            OpenProjectUrlCommand = new RelayCommand(OpenProjectUrl);
            InstallIntoProjectsCommand = new AsyncRelayCommand(InstallIntoCheckedProjectsAsync);

            // 3 dedicated commands for the projects grid
            InstallCheckedCommand   = new AsyncRelayCommand(InstallIntoCheckedAsync);
            UpdateCheckedCommand    = new AsyncRelayCommand(UpdateIntoCheckedAsync);
            UninstallCheckedCommand = new AsyncRelayCommand(UninstallFromCheckedAsync);

            // Settings Commands
            AddPackageSourceCommand = new RelayCommand(AddPackageSource);
            EditPackageSourceCommand = new RelayCommand(EditPackageSource);
            RemovePackageSourceCommand = new RelayCommand(RemovePackageSource);
            SavePackageSourceCommand = new RelayCommand(SavePackageSource);
            CancelAddSourceCommand = new RelayCommand(CancelAddSource);
            BrowseSourcePathCommand = new AsyncRelayCommand(BrowseSourcePathAsync);

            // Load popular packages by default ONLY if online
            if (_nugetService.IsOnline)
                _ = LoadPopularPackagesAsync();
            else
                StatusMessage = "Offline Mode - NuGet.org is unavailable";

            // Load package sources
            LoadPackageSources();

            // اختيار المصدر الافتراضي (nuget.org)
            SelectedSearchSource = PackageSources.FirstOrDefault();

            // Load all projects from Shell.SolutionTree
            LoadProjectEntries();

            // Update no-project mode based on loaded projects
            if (ProjectEntries.Count > 0)
            {
                IsNoProjectMode = false;
                _projectName = $"Solution ({ProjectEntries.Count} projects)";
            }
        }

        public NuGetPackageManagerViewModel(string projectPath) : this()
        {

            
            _projectPath = projectPath;
            _projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath);
            IsNoProjectMode = string.IsNullOrEmpty(projectPath); // تحديث حالة المشروع
        }
        public NuGetPackageManagerViewModel(SolutionNode node) : this()
        {
            // Load all projects from the given node (Solution or Project)
            LoadProjectEntries(node);
        }
        partial void OnSelectedSearchSourceChanged(PackageSourceInfo? value)
        {
            // إعادة البحث عند تغيير المصدر
            if (value != null && IsBrowseTabSelected)
            {

                _ = SearchPackagesAsync();
            }
        }

        partial void OnResultsPerPageChanged(int value)
        {
            // إعادة البحث عند تغيير عدد النتائج
            if (IsBrowseTabSelected)
            {
                _ = SearchPackagesAsync();
            }
        }

        partial void OnIncludePrereleaseChanged(bool value)
        {
            // إعادة البحث عند تغيير Include Prerelease
            if (IsBrowseTabSelected)
            {
                _ = SearchPackagesAsync();
            }
        }

        partial void OnIsBrowseTabSelectedChanged(bool value)
        {
            if (value)
            {
                _ = LoadPopularPackagesAsync();
                ProjectsActionButtonText = "Install into selected projects";
                ProjectsActionButtonClass = "PrimaryButton";
            }
        }

        partial void OnIsInstalledTabSelectedChanged(bool value)
        {
            if (value)
            {
                _ = LoadInstalledPackagesAsync();
                ProjectsActionButtonText = "Uninstall from selected projects";
                ProjectsActionButtonClass = "UninstallButton";
            }
        }

        partial void OnIsUpdatesTabSelectedChanged(bool value)
        {
            if (value)
            {
                _ = LoadUpdatesAsync();
                ProjectsActionButtonText = "Update selected projects";
                ProjectsActionButtonClass = "UpdateButton";
            }
        }

        partial void OnIsSettingsTabSelectedChanged(bool value)
        {
            if (value)
            {
                LoadPackageSources();
            }
        }

        partial void OnNewSourceTypeIndexChanged(int value)
        {
            // 0 = Online, 1 = Local, 2 = Network
            IsLocalSourceType = value == 1 || value == 2;
        }

        partial void OnSelectedPackageChanged(NuGetPackageInfo? value)
        {
            if (value != null)
            {
                // استخدام LatestVersion كـ default للـ ComboBox
                SelectedVersion = value.LatestVersion;
                InstallButtonText = value.IsInstalled ? "Uninstall" : "Install";

                // Refresh project entries only when not installing
                if (!IsInstalling)
                    _ = RefreshProjectEntriesAsync(value);
            }
        }

        // ── Project Install Grid ──────────────────────────────────────────────

        /// <summary>Load all .csproj files from Shell.SolutionTree (no-arg version).</summary>
        private void LoadProjectEntries()
        {
            ProjectEntries.Clear();
            try
            {
                var solutionTree = MainWindowViewModel.Instance?.SolutionTree;
                if (solutionTree == null || solutionTree.Count == 0) return;

                foreach (var root in solutionTree)
                    CollectProjects(root);
            }
            catch { }
        }

        /// <summary>Collect all .csproj files from the given solution or project node.</summary>
        public void LoadProjectEntries(SolutionNode node)
        {
            ProjectEntries.Clear();
            try
            {
                CollectProjects(node);
            }
            catch { }

            // Update mode
            if (ProjectEntries.Count > 0)
            {
                IsNoProjectMode = false;
                ProjectName = node.Name;
                
                // If it's a single project, set its name as the title
                if (ProjectEntries.Count == 1)
                    ProjectName = ProjectEntries[0].ProjectName;
            }
        }

        private void CollectProjects(SolutionNode node)
        {
            if (node.Kind == SolutionNodeKind.Project &&
                !string.IsNullOrEmpty(node.FilePath) &&
                node.FilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                var entry = new ProjectInstallEntry
                {
                    ProjectName = node.Name ?? Path.GetFileNameWithoutExtension(node.FilePath),
                    ProjectPath = node.FilePath,
                    InstalledVersion = null,
                    SelectedVersion = SelectedVersion ?? SelectedPackage?.LatestVersion
                };
                ProjectEntries.Add(entry);
            }
            foreach (var child in node.Children)
                CollectProjects(child);
        }

        /// <summary>After selecting a package, check which projects already have it installed.</summary>
        private async Task RefreshProjectEntriesAsync(NuGetPackageInfo pkg)
        {
            // استخدام الإصدار المحدد في ComboBox أو LatestVersion كـ fallback
            var targetVersion = SelectedVersion ?? pkg.LatestVersion;

            await Task.Run(() =>
            {
                foreach (var entry in ProjectEntries)
                {
                    entry.SelectedVersion = targetVersion;
                    try
                    {
                        // قراءة محلية سريعة بدون استدعاء NuGet.org
                        var installed = _nugetService.GetInstalledPackagesLocal(entry.ProjectPath);
                        var found = installed.FirstOrDefault(p =>
                            string.Equals(p.Id, pkg.Id, StringComparison.OrdinalIgnoreCase));
                        entry.InstalledVersion = found?.InstalledVersion;

                        // في تبويب Updates: حدد تلقائياً المشاريع التي لديها نسخة قديمة
                        if (IsUpdatesTabSelected && found != null &&
                            found.InstalledVersion != pkg.LatestVersion)
                            entry.IsChecked = true;
                        else
                            entry.IsChecked = false;
                    }
                    catch { entry.InstalledVersion = null; entry.IsChecked = false; }
                }
            });
        }

        /// <summary>Install, update, or uninstall the selected package in all checked projects.</summary>
        private async Task InstallIntoCheckedProjectsAsync()
        {
            if (SelectedPackage == null) return;

            var targets = ProjectEntries.Where(e => e.IsChecked).ToList();
            if (!targets.Any())
            {
                StatusMessage = "⚠️ No projects selected";
                return;
            }

            IsInstalling = true;
            int ok = 0, fail = 0;

            foreach (var entry in targets)
            {
                try
                {
                    if (IsInstalledTabSelected)
                    {
                        // Uninstall
                        LoadingMessage = $"Uninstalling {SelectedPackage.Id} from {entry.ProjectName}…";
                        await _nugetService.UninstallPackageAsync(entry.ProjectPath, SelectedPackage.Id);
                        entry.InstalledVersion = null;
                    }
                    else if (IsUpdatesTabSelected)
                    {
                        // Update
                        var ver = entry.SelectedVersion ?? SelectedPackage.LatestVersion;
                        LoadingMessage = $"Updating {SelectedPackage.Id} → {ver} in {entry.ProjectName}…";
                        await _nugetService.UpdatePackageAsync(entry.ProjectPath, SelectedPackage.Id, ver);
                        entry.InstalledVersion = ver;
                    }
                    else
                    {
                        // Install or update
                        var ver = entry.SelectedVersion ?? SelectedPackage.LatestVersion;
                        var isUpdate = !string.IsNullOrEmpty(entry.InstalledVersion) &&
                                       entry.InstalledVersion != ver;
                        LoadingMessage = isUpdate
                            ? $"Updating {SelectedPackage.Id} → {ver} in {entry.ProjectName}…"
                            : $"Installing {SelectedPackage.Id} {ver} → {entry.ProjectName}…";

                        if (isUpdate)
                            await _nugetService.UpdatePackageAsync(entry.ProjectPath, SelectedPackage.Id, ver);
                        else
                            await _nugetService.InstallPackageAsync(
                                entry.ProjectPath, SelectedPackage.Id, ver,
                                SelectedSearchSource?.Source);
                        entry.InstalledVersion = ver;
                    }

                    entry.IsChecked = false;
                    ok++;
                }
                catch (Exception ex)
                {
                    MainWindowViewModel.ReportException(ex);
                    fail++;
                }
            }

            IsInstalling = false;
            StatusMessage = fail == 0
                ? $"✅ Done: {ok} project(s)"
                : $"✅ {ok} succeeded  ❌ {fail} failed";

            // Refresh the list after operation
            if (IsInstalledTabSelected) await LoadInstalledPackagesAsync();
            else if (IsUpdatesTabSelected) await LoadUpdatesAsync();
        }

        // ── 3 dedicated project-grid commands ────────────────────────────────

        private async Task InstallIntoCheckedAsync()
        {
            if (SelectedPackage == null) return;
            var targets = ProjectEntries.Where(e => e.IsChecked).ToList();
            if (!targets.Any()) { StatusMessage = "⚠️ No projects selected"; return; }

            // الإصدار المحدد من ComboBox الرئيسي
            var ver = SelectedVersion ?? SelectedPackage.LatestVersion;

            IsInstalling = true;
            int ok = 0, fail = 0;
            foreach (var entry in targets)
            {
                LoadingMessage = $"Installing {SelectedPackage.Id} {ver} → {entry.ProjectName}…";
                try
                {
                    await _nugetService.InstallPackageAsync(
                        entry.ProjectPath, SelectedPackage.Id, ver, SelectedSearchSource?.Source);
                    entry.InstalledVersion = ver;
                    entry.SelectedVersion = ver;
                    entry.IsChecked = false;
                    ok++;
                }
                catch (Exception ex) { MainWindowViewModel.ReportException(ex); fail++; }
            }
            IsInstalling = false;
            StatusMessage = fail == 0 ? $"✅ Installed into {ok} project(s)" : $"✅ {ok}  ❌ {fail}";
        }

        private async Task UpdateIntoCheckedAsync()
        {
            if (SelectedPackage == null) return;
            var targets = ProjectEntries.Where(e => e.IsChecked).ToList();
            if (!targets.Any()) { StatusMessage = "⚠️ No projects selected"; return; }

            // الإصدار المحدد من ComboBox الرئيسي
            var ver = SelectedVersion ?? SelectedPackage.LatestVersion;

            IsInstalling = true;
            int ok = 0, fail = 0;
            foreach (var entry in targets)
            {
                LoadingMessage = $"Updating {SelectedPackage.Id} → {ver} in {entry.ProjectName}…";
                try
                {
                    await _nugetService.UpdatePackageAsync(entry.ProjectPath, SelectedPackage.Id, ver);
                    entry.InstalledVersion = ver;
                    entry.SelectedVersion = ver;
                    entry.IsChecked = false;
                    ok++;
                }
                catch (Exception ex) { MainWindowViewModel.ReportException(ex); fail++; }
            }
            IsInstalling = false;
            StatusMessage = fail == 0 ? $"✅ Updated {ok} project(s)" : $"✅ {ok}  ❌ {fail}";
        }

        private async Task UninstallFromCheckedAsync()
        {
            if (SelectedPackage == null) return;
            var targets = ProjectEntries.Where(e => e.IsChecked).ToList();
            if (!targets.Any()) { StatusMessage = "⚠️ No projects selected"; return; }

            IsInstalling = true;
            int ok = 0, fail = 0;
            foreach (var entry in targets)
            {
                LoadingMessage = $"Uninstalling {SelectedPackage.Id} from {entry.ProjectName}…";
                try
                {
                    await _nugetService.UninstallPackageAsync(entry.ProjectPath, SelectedPackage.Id);
                    entry.InstalledVersion = null;
                    entry.IsChecked = false;
                    ok++;
                }
                catch (Exception ex) { MainWindowViewModel.ReportException(ex); fail++; }
            }
            IsInstalling = false;

            // إذا تم الحذف من جميع المشاريع → احذف من القائمة مباشرة
            if (ok > 0 && fail == 0)
            {
                var pkg = SelectedPackage;
                SelectedPackage = null;
                Packages.Remove(pkg);
                StatusMessage = $"✅ Uninstalled from {ok} project(s) — {Packages.Count} packages remaining";
            }
            else
            {
                StatusMessage = $"✅ {ok} succeeded  ❌ {fail} failed";
            }
        }

        private async Task LoadPopularPackagesAsync()
        {
            try
            {
                IsLoading = true;
                LoadingMessage = "Loading packages...";
                StatusMessage = "Loading...";

                var sourceUrl = SelectedSearchSource?.Source;

                // If "All Sources" is selected, search in all sources
                if (sourceUrl == "ALL")
                {
                    var allPackages = new List<NuGetPackageInfo>();

                    // Search in nuget.org first
                    var onlinePackages = await _nugetService.SearchPackagesAsync("", null, 0, ResultsPerPage, IncludePrerelease);
                    allPackages.AddRange(onlinePackages);

                    // Search in all local sources
                    var sources = _packageSourcesService.LoadPackageSources();
                    foreach (var source in sources.Where(s => s.Type == "Local" && s.IsEnabled))
                    {
                        try
                        {
                            var localPackages = await _nugetService.SearchPackagesAsync("", source.Source, 0, ResultsPerPage, IncludePrerelease);
                            allPackages.AddRange(localPackages);
                        }
                        catch (Exception ex)
                        {
                            MainWindowViewModel.ReportException(new Exception(ex.Message));
                        }
                    }

                    Packages = new ObservableCollection<NuGetPackageInfo>(allPackages);
                }
                else
                {
                    // Search in selected source only
                    var packages = await _nugetService.SearchPackagesAsync("", sourceUrl, 0, ResultsPerPage, IncludePrerelease);
                    Packages = new ObservableCollection<NuGetPackageInfo>(packages);
                }

                StatusMessage = $"Found {Packages.Count} packages";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SearchPackagesAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                await LoadPopularPackagesAsync();
                return;
            }

            try
            {
                IsLoading = true;
                LoadingMessage = $"Searching for '{SearchQuery}'...";
                StatusMessage = "Searching...";

                var sourceUrl = SelectedSearchSource?.Source;

                // If "All Sources" is selected, search in all sources
                if (sourceUrl == "ALL")
                {
                    var allPackages = new List<NuGetPackageInfo>();

                    // Search in nuget.org first
                    var onlinePackages = await _nugetService.SearchPackagesAsync(SearchQuery, null, 0, ResultsPerPage, IncludePrerelease);
                    allPackages.AddRange(onlinePackages);

                    // Search in all local sources
                    var sources = _packageSourcesService.LoadPackageSources();
                    foreach (var source in sources.Where(s => s.Type == "Local" && s.IsEnabled))
                    {
                        try
                        {
                            var localPackages = await _nugetService.SearchPackagesAsync(SearchQuery, source.Source, 0, ResultsPerPage, IncludePrerelease);
                            allPackages.AddRange(localPackages);
                        }
                        catch (Exception ex)
                        {
                            MainWindowViewModel.ReportException(new Exception(ex.Message));
                        }
                    }

                    Packages = new ObservableCollection<NuGetPackageInfo>(allPackages);
                }
                else
                {
                    // Search in selected source only
                    var packages = await _nugetService.SearchPackagesAsync(SearchQuery, sourceUrl, 0, ResultsPerPage, IncludePrerelease);
                    Packages = new ObservableCollection<NuGetPackageInfo>(packages);
                }

                StatusMessage = $"Found {Packages.Count} packages";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadInstalledPackagesAsync()
        {
            // إذا كان هناك مشاريع محمّلة من الـ Solution، نجمع الحزم من جميعها
            if (ProjectEntries.Count > 0)
            {
                try
                {
                    IsLoading = true;
                    LoadingMessage = "Loading installed packages...";
                    StatusMessage = "Loading...";

                    var allPackages = new Dictionary<string, NuGetPackageInfo>(StringComparer.OrdinalIgnoreCase);

                        foreach (var entry in ProjectEntries)
                        {
                            try
                            {
                            var pkgs = await _nugetService.GetInstalledPackagesAsync(entry.ProjectPath);
                                foreach (var pkg in pkgs)
                                {
                                // إذا الحزمة موجودة بالفعل، نتجاهلها (نعرض كل حزمة مرة واحدة)
                                    if (!allPackages.ContainsKey(pkg.Id))
                                        allPackages[pkg.Id] = pkg;
                                }
                            }
                            catch { }
                        }

                    Packages = new ObservableCollection<NuGetPackageInfo>(
                        allPackages.Values.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase));
                    StatusMessage = $"{Packages.Count} packages installed";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error: {ex.Message}";
                }
                finally
                {
                    IsLoading = false;
                }
                return;
            }

            // وضع مشروع واحد
            if (string.IsNullOrEmpty(_projectPath))
            {
                StatusMessage = "⚠️ Please open a project first to view installed packages";
                Packages.Clear();
                return;
            }

            try
            {
                IsLoading = true;
                LoadingMessage = "Loading installed packages...";
                StatusMessage = "Loading...";

                var packages = await _nugetService.GetInstalledPackagesAsync(_projectPath);
                Packages = new ObservableCollection<NuGetPackageInfo>(
                    packages.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase));
                StatusMessage = $"{Packages.Count} packages installed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MainWindowViewModel.ReportException(new Exception(ex.Message));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadUpdatesAsync()
        {
            // إذا كان هناك مشاريع محمّلة من الـ Solution، نجمع التحديثات من جميعها
            if (ProjectEntries.Count > 0)
            {
                try
                {
                    IsLoading = true;
                    LoadingMessage = "Checking for updates...";
                    StatusMessage = "Checking...";

                    var allUpdates = new Dictionary<string, NuGetPackageInfo>(StringComparer.OrdinalIgnoreCase);

                    foreach (var entry in ProjectEntries)
                    {
                        try
                        {
                            var pkgs = await _nugetService.GetUpdatesAsync(entry.ProjectPath);
                            foreach (var pkg in pkgs)
                            {
                                if (!allUpdates.ContainsKey(pkg.Id))
                                    allUpdates[pkg.Id] = pkg;
                            }
                        }
                        catch { }
                    }

                    Packages = new ObservableCollection<NuGetPackageInfo>(
                        allUpdates.Values.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase));
                    StatusMessage = $"{Packages.Count} updates available";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error: {ex.Message}";
                }
                finally
                {
                    IsLoading = false;
                }
                return;
            }

            // وضع مشروع واحد
            if (string.IsNullOrEmpty(_projectPath))
            {
                StatusMessage = "⚠️ Please open a project first to check for updates";
                Packages.Clear();
                return;
            }

            try
            {
                IsLoading = true;
                LoadingMessage = "Checking for updates...";
                StatusMessage = "Checking...";

                var packages = await _nugetService.GetUpdatesAsync(_projectPath);
                Packages = new ObservableCollection<NuGetPackageInfo>(
                    packages.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase));
                StatusMessage = $"{Packages.Count} updates available";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task InstallPackageAsync()
        {
            if (SelectedPackage == null || string.IsNullOrEmpty(SelectedVersion))
                return;

            // التحقق من وجود مشروع
            if (string.IsNullOrEmpty(_projectPath))
            {
                StatusMessage = "⚠️ Please open a project first to install packages";
                return;
            }

            try
            {
                IsInstalling = true;
                LoadingMessage = $"Installing {SelectedPackage.Id} {SelectedVersion}...";
                StatusMessage = "Installing...";

                // Update the package in-place without reloading the whole list
                if (SelectedPackage.IsInstalled)
                {
                    await _nugetService.UninstallPackageAsync(_projectPath, SelectedPackage.Id);
                    SelectedPackage.IsInstalled = false;
                    SelectedPackage.InstalledVersion = null;
                    InstallButtonText = "Install";
                    StatusMessage = $"Uninstalled {SelectedPackage.Id}";
                }
                else
                {
                    var sourceUrl = SelectedSearchSource?.Source;
                    await _nugetService.InstallPackageAsync(_projectPath, SelectedPackage.Id, SelectedVersion, sourceUrl);
                    SelectedPackage.IsInstalled = true;
                    SelectedPackage.InstalledVersion = SelectedVersion;
                    InstallButtonText = "Uninstall";
                    StatusMessage = $"Installed {SelectedPackage.Id} {SelectedVersion}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
            }
        }

        private async Task UninstallPackageAsync()
        {
            if (SelectedPackage == null)
                return;

            try
            {
                IsInstalling = true;
                LoadingMessage = $"Uninstalling {SelectedPackage.Id}...";
                StatusMessage = "Uninstalling...";

                await _nugetService.UninstallPackageAsync(_projectPath, SelectedPackage.Id);
                StatusMessage = $"Uninstalled {SelectedPackage.Id}";

                // Remove from list in-place instead of full reload
                var pkg = SelectedPackage;
                SelectedPackage = null;
                Packages.Remove(pkg);
                StatusMessage = $"Uninstalled {pkg.Id} — {Packages.Count} packages installed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
            }
        }

        private async Task UpdatePackageAsync()
        {
            if (SelectedPackage == null || string.IsNullOrEmpty(SelectedVersion))
                return;

            try
            {
                IsInstalling = true;
                LoadingMessage = $"Updating {SelectedPackage.Id} to {SelectedVersion}...";
                StatusMessage = "Updating...";

                await _nugetService.UpdatePackageAsync(_projectPath, SelectedPackage.Id, SelectedVersion);
                SelectedPackage.InstalledVersion = SelectedVersion;
                StatusMessage = $"Updated {SelectedPackage.Id} to {SelectedVersion}";
                // Don't reload the list — just update in-place
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
            }
        }

        private void OpenProjectUrl()
        {
            if (SelectedPackage?.ProjectUrl != null)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = SelectedPackage.ProjectUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error opening URL: {ex.Message}";
                }
            }
        }
        [RelayCommand]
        private void Close()
        {
            this.Close();
        }

        private async Task RefreshCurrentTabAsync()
        {
            try
            {
                if (IsBrowseTabSelected)
                {
                    await LoadPopularPackagesAsync();
                }
                else if (IsInstalledTabSelected)
                {
                    await LoadInstalledPackagesAsync();
                }
                else if (IsUpdatesTabSelected)
                {
                    await LoadUpdatesAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error refreshing: {ex.Message}";
            }
        }

        private async Task UpdateAllPackagesAsync()
        {
            if (!Packages.Any())
            {
                StatusMessage = "No packages to update";
                return;
            }

            // Get only selected packages in Updates tab
            var packagesToUpdate = IsUpdatesTabSelected
                ? Packages.Where(p => p.IsSelected).ToList()
                : Packages.ToList();

            if (!packagesToUpdate.Any())
            {
                StatusMessage = "⚠️ No packages selected for update";
                return;
            }

            try
            {
                IsInstalling = true;
                var totalPackages = packagesToUpdate.Count;
                var currentPackage = 0;
                var successCount = 0;
                var failCount = 0;

                foreach (var package in packagesToUpdate)
                {
                    currentPackage++;
                    LoadingMessage = $"Updating {package.Id} ({currentPackage}/{totalPackages})...";
                    StatusMessage = $"Updating package {currentPackage} of {totalPackages}...";

                    try
                    {
                        await _nugetService.UpdatePackageAsync(_projectPath, package.Id, package.LatestVersion);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        // Continue with other packages even if one fails
                        failCount++;
                        MainWindowViewModel.ReportException(new Exception($"Failed to update {package.Id}: {ex.Message}"));
                    }
                }

                if (failCount > 0)
                {
                    StatusMessage = $"✅ Updated {successCount} packages, ❌ {failCount} failed";
                }
                else
                {
                    StatusMessage = $"✅ Updated {successCount} packages successfully";
                }

                // Refresh the updates list
                await LoadUpdatesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating packages: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
            }
        }

        // Settings Methods
        private void LoadPackageSources()
        {
            PackageSources.Clear();

            // Add "All Sources" option at the beginning (searches all sources)
            PackageSources.Add(new PackageSourceInfo
            {
                Name = "All Sources",
                Source = "ALL", // Special marker to search all sources
                Type = "All",
                IsEnabled = true
            });

            // Load sources from PackageSourcesService
            var sources = _packageSourcesService.LoadPackageSources();

            foreach (var source in sources)
            {
                PackageSources.Add(new PackageSourceInfo
                {
                    Name = source.Name,
                    Source = source.Source,
                    Type = source.Type,
                    IsEnabled = source.IsEnabled
                });
            }


        }

        private void AddPackageSource()
        {
            IsAddingSource = true;
            NewSourceName = "";
            NewSourcePath = "";
            NewSourceTypeIndex = 0;
        }

        private void EditPackageSource()
        {
            if (SelectedPackageSource == null) return;

            IsAddingSource = true;
            NewSourceName = SelectedPackageSource.Name;
            NewSourcePath = SelectedPackageSource.Source;

            // Set type index based on source type
            NewSourceTypeIndex = SelectedPackageSource.Type switch
            {
                "Online" => 0,
                "Local" => 1,
                "Network" => 2,
                _ => 0
            };
        }

        private void RemovePackageSource()
        {
            if (SelectedPackageSource == null) return;

            try
            {
                // Convert to PackageSource model
                var source = new PackageSource(
                    SelectedPackageSource.Name,
                    SelectedPackageSource.Source,
                    SelectedPackageSource.Type,
                    SelectedPackageSource.IsEnabled
                );

                // Get all sources
                var allSources = PackageSources.Select(ps => new PackageSource(
                    ps.Name, ps.Source, ps.Type, ps.IsEnabled
                )).ToList();

                // Remove using service
                _packageSourcesService.RemovePackageSource(source, allSources);

                // Update UI
                PackageSources.Remove(SelectedPackageSource);
                StatusMessage = $"✅ Removed source: {SelectedPackageSource.Name}";


            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
                MainWindowViewModel.ReportException(new Exception(ex.Message));
            }
        }

        private void SavePackageSource()
        {
            if (string.IsNullOrWhiteSpace(NewSourceName) || string.IsNullOrWhiteSpace(NewSourcePath))
            {
                StatusMessage = "⚠️ Please enter both name and path/URL";
                return;
            }

            var sourceType = NewSourceTypeIndex switch
            {
                0 => "Online",
                1 => "Local",
                2 => "Network",
                _ => "Online"
            };

            try
            {
                // Validate source
                if (!_packageSourcesService.ValidateSource(NewSourcePath, sourceType))
                {
                    StatusMessage = $"❌ Invalid {sourceType} source path/URL";
                    return;
                }

                var newSource = new PackageSource(NewSourceName, NewSourcePath, sourceType, true);

                // Get all sources
                var allSources = PackageSources.Select(ps => new PackageSource(
                    ps.Name, ps.Source, ps.Type, ps.IsEnabled
                )).ToList();

                // Check if editing existing source
                if (SelectedPackageSource != null && PackageSources.Contains(SelectedPackageSource))
                {
                    var oldSource = new PackageSource(
                        SelectedPackageSource.Name,
                        SelectedPackageSource.Source,
                        SelectedPackageSource.Type,
                        SelectedPackageSource.IsEnabled
                    );

                    _packageSourcesService.UpdatePackageSource(oldSource, newSource, allSources);

                    var index = PackageSources.IndexOf(SelectedPackageSource);
                    PackageSources[index] = new PackageSourceInfo
                    {
                        Name = NewSourceName,
                        Source = NewSourcePath,
                        Type = sourceType,
                        IsEnabled = true
                    };

                    StatusMessage = $"✅ Updated source: {NewSourceName}";

                }
                else
                {
                    _packageSourcesService.AddPackageSource(newSource, allSources);

                    PackageSources.Add(new PackageSourceInfo
                    {
                        Name = NewSourceName,
                        Source = NewSourcePath,
                        Type = sourceType,
                        IsEnabled = true
                    });

                    StatusMessage = $"✅ Added source: {NewSourceName}";

                }

                IsAddingSource = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
                MainWindowViewModel.ReportException(new Exception(ex.Message));
            }
        }

        private void CancelAddSource()
        {
            IsAddingSource = false;
            NewSourceName = "";
            NewSourcePath = "";
            NewSourceTypeIndex = 0;
        }

        private async Task BrowseSourcePathAsync()
        {
            try
            {
                var folderDialog = new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Select Package Source Folder",
                    AllowMultiple = false
                };

                // Get the main window
                var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (mainWindow != null)
                {
                    var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(folderDialog);

                    if (result.Count > 0)
                    {
                        NewSourcePath = result[0].Path.LocalPath;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error browsing folder: {ex.Message}";
            }
        }
    }

    // Package Source Info Model
    public partial class PackageSourceInfo : ObservableObject
    {
        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private string _source = "";

        [ObservableProperty]
        private string _type = "Online";

        [ObservableProperty]
        private bool _isEnabled = true;
    }

}
