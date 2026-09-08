using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Dock.Model.Core;
using Scadix.AxamlDesigner;
using Scadix.Designer.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scadix.Designer;

public partial class MainWindow : Window
{
    public static MainWindow? Instance;

    // Public properties to access UI elements
    public StatusBarView? StatusBar => this.FindControl<StatusBarView>("StatusBarView");
    public ToolbarView? MainToolbar => this.FindControl<ToolbarView>("ToolbarView");


    // Cache for hidden dockables to restore them later
    private Dictionary<string, (IDock toolDock, IDockable dockable)> _hiddenDockables
        = new Dictionary<string, (IDock, IDockable)>();

    public MainWindow()
    {
        Instance    = this;
        DataContext = MainWindowViewModel.Instance;

        RenameCommands();
        BasicMetadata.Register();

        AvaloniaXamlLoader.Load(this);

        this.Loaded += OnWindowLoaded;

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent,  OnDragOver);
        AddHandler(DragDrop.DropEvent,      OnDrop);

        RouteDesignSurfaceCommands();
        Scadix.AxamlDesigner.ExtensionMethods.AddCommandHandler(
            this, RefreshCommand,
            MainWindowViewModel.Instance.Refresh,
            MainWindowViewModel.Instance.CanRefresh);

        // Add keyboard shortcuts for debugging
        SetupDebugKeyboardShortcuts();

        LoadSettings();
        ProcessPaths(App.Args ?? Array.Empty<string>());

        // Initialize menu check states after layout is ready
        this.Loaded += InitializeMenuCheckStates;
    }

    private void SetupDebugKeyboardShortcuts()
    {
        KeyDown += OnMainWindowKeyDown;
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var toolbar = MainToolbar;
        if (toolbar == null) return;

        // F5 - Start Debugging or Continue
        if (e.Key == Key.F5 && e.KeyModifiers == KeyModifiers.None)
        {
            // Check if debugging is active
            var btnStartDebug = toolbar.FindControl<Button>("BtnStartDebug");
            var btnContinue = toolbar.FindControl<Button>("BtnContinue");

            if (btnContinue?.IsEnabled == true)
            {
                // Already debugging - continue
                btnContinue.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            else if (btnStartDebug?.IsEnabled == true)
            {
                // Start debugging
                btnStartDebug.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            e.Handled = true;
        }
        // Shift+F5 - Stop Debugging
        else if (e.Key == Key.F5 && e.KeyModifiers == KeyModifiers.Shift)
        {
            var btnStopDebug = toolbar.FindControl<Button>("BtnStopDebug");
            if (btnStopDebug?.IsEnabled == true)
            {
                btnStopDebug.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            e.Handled = true;
        }
        // Ctrl+Shift+F5 - Restart Debugging
        else if (e.Key == Key.F5 && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            var btnRestartDebug = toolbar.FindControl<Button>("BtnRestartDebug");
            if (btnRestartDebug?.IsEnabled == true)
            {
                btnRestartDebug.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            e.Handled = true;
        }
        // F10 - Step Over
        else if (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.None)
        {
            var btnStepOver = toolbar.FindControl<Button>("BtnStepOver");
            if (btnStepOver?.IsEnabled == true)
            {
                btnStepOver.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            e.Handled = true;
        }
        // F11 - Step Into
        else if (e.Key == Key.F11 && e.KeyModifiers == KeyModifiers.None)
        {
            var btnStepInto = toolbar.FindControl<Button>("BtnStepInto");
            if (btnStepInto?.IsEnabled == true)
            {
                btnStepInto.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            e.Handled = true;
        }
        // Shift+F11 - Step Out
        else if (e.Key == Key.F11 && e.KeyModifiers == KeyModifiers.Shift)
        {
            var btnStepOut = toolbar.FindControl<Button>("BtnStepOut");
            if (btnStepOut?.IsEnabled == true)
            {
                btnStepOut.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            e.Handled = true;
        }
        // F9 - Toggle Breakpoint on current line
        else if (e.Key == Key.F9 && e.KeyModifiers == KeyModifiers.None)
        {
            ToggleBreakpointOnCurrentLine();
            e.Handled = true;
        }
    }

    private void ToggleBreakpointOnCurrentLine()
    {
        var currentDoc = MainWindowViewModel.Instance.CurrentDocument;
        if (currentDoc == null || !MainWindowViewModel.Instance.Views.ContainsKey(currentDoc)) return;

        var docView = MainWindowViewModel.Instance.Views[currentDoc] as DocumentView;
        var xamlEditor = docView?.uxXamlEditor;
        var margin = xamlEditor?.BreakpointMargin;
        var editor = xamlEditor?.Editor;

        if (margin != null && editor != null)
        {
            var currentLine = editor.TextArea.Caret.Line;
            margin.ToggleBreakpoint(currentLine);
        }
    }
    

    // ── Window Loaded ────────────────────────────────────────────────────

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        var propsView = this.FindControl<PropertiesToolView>("uxPropertiesToolView")
                     ?? FindDescendant<PropertiesToolView>(this);
        MainWindowViewModel.Instance.PropertyGrid = propsView?.PropertyGrid;
    }

    private static T? FindDescendant<T>(Control root) where T : Control
    {
        foreach (var child in root.GetVisualDescendants())
            if (child is T match) return match;
        return null;
    }

    // ── Drag & Drop ──────────────────────────────────────────────────────

    private void OnDragEnter(object? sender, DragEventArgs e) => ProcessDrag(e);
    private void OnDragOver (object? sender, DragEventArgs e) => ProcessDrag(e);
    private void OnDrop     (object? sender, DragEventArgs e) { }

    private static void ProcessDrag(DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        e.Handled     = true;
    }

    private static void ProcessPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (path.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) ||
                path.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
                Toolbox.Instance.AddAssembly(path);
            else if (path.EndsWith(".xaml", StringComparison.InvariantCultureIgnoreCase) ||
                     path.EndsWith(".axaml", StringComparison.InvariantCultureIgnoreCase) ||
                     path.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase))
                MainWindowViewModel.Instance.Open(path);
        }
    }

    // ── File dialogs ─────────────────────────────────────────────────────

    public async Task<string?> AskOpenFileName()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title          = "Open XAML Document",
                AllowMultiple  = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("XAML Documents") { Patterns = new[] { "*.xml", "*.xaml", "*.axaml" } }
                }
            });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> AskOpenSolutionFileName()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title          = "Open Solution or Project",
                AllowMultiple  = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Solution & Project Files")
                        { Patterns = new[] { "*.sln", "*.slnx", "*.csproj" } },
                    new FilePickerFileType("Solution Files")
                        { Patterns = new[] { "*.sln", "*.slnx" } },
                    new FilePickerFileType("Project Files")
                        { Patterns = new[] { "*.csproj" } }
                }
            });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> AskOpenFolderName()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title         = "Open Folder",
                AllowMultiple = false
            });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public async Task<string?> AskSaveFileName(string initName)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title             = "Save XAML Document",
                SuggestedFileName = initName,
                FileTypeChoices   = new[]
                {
                    new FilePickerFileType("XAML Documents") { Patterns = new[] { "*.xml", "*.xaml", "*.axaml" } }
                }
            });

        return file?.Path.LocalPath;
    }

    // ── Settings ─────────────────────────────────────────────────────────

    private static void LoadSettings()
    {
        // Apply UI visibility settings
        var s = Scadix.Designer.Settings.Default;
        var mainWindow = Instance;
        
        if (mainWindow != null)
        {
            var statusBar = mainWindow.StatusBar;
            if (statusBar != null) statusBar.IsVisible = s.ShowStatusBar;
            
            var toolbar = mainWindow.MainToolbar;
            if (toolbar != null) toolbar.IsVisible = s.ShowMainToolbar;
        }
    }

    private void RecentFiles_Click(object? sender, RoutedEventArgs e)
    {
        if (e.Source is MenuItem mi && mi.Header is string path)
            MainWindowViewModel.Instance.Open(path);
    }

    private void OpenProject_Click(object? sender, RoutedEventArgs e)
        => MainWindowViewModel.Instance.OpenSolutionDialog();

    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
        => MainWindowViewModel.Instance.OpenFolderDialog();

    private void CloseProject_Click(object? sender, RoutedEventArgs e)
    {
        MainWindowViewModel.Instance.CloseProject();
    }

    private void ShowSolution_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: bring Solution Explorer tool window to front
    }

    private async void OpenSettings_Click(object? sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow();
        await win.ShowDialog(this);
    }

    private void GitCommit_Click(object? sender, RoutedEventArgs e)
    {
        MainWindowViewModel.Instance.Factory.OpenGitManager();
        MainWindowViewModel.Instance.Factory.Git.SelectedTabIndex = 1; // Switch to Local Changes tab
    }

    private async void GitPush_Click(object? sender, RoutedEventArgs e)
    {
        MainWindowViewModel.Instance.Factory.OpenGitManager();
        await MainWindowViewModel.Instance.Factory.Git.PushAsync();
    }

    private async void GitPull_Click(object? sender, RoutedEventArgs e)
    {
        MainWindowViewModel.Instance.Factory.OpenGitManager();
        await MainWindowViewModel.Instance.Factory.Git.PullAsync();
    }

    private void GitCreateRepo_Click(object? sender, RoutedEventArgs e)
    {
        var shell = MainWindowViewModel.Instance;
        var projectPath = shell.Factory.Git.GetProjectFolder();
        if (!string.IsNullOrEmpty(projectPath))
            shell.Factory.OpenCreateGitRepository(projectPath);
    }

    private void GitRepositories_Click(object? sender, RoutedEventArgs e)
    {
        MainWindowViewModel.Instance.Factory.OpenGitRepositories();
    }

    private void GitManager_Click(object? sender, RoutedEventArgs e)
    {
        MainWindowViewModel.Instance.Factory.OpenGitManager();
    }

    private async void GitShareGitHub_Click(object? sender, RoutedEventArgs e)
    {
        // Simple input dialog for GitHub URL (simulated for now with a prompt if we had one)
        // For now, let's just ask the Shell to handle it
        var msg = "Please enter the GitHub repository URL to share this project:";
        // In a real app we'd show a TextBox dialog. Let's just use the current clipboard or a default.
        var url = await Clipboard.GetTextAsync();
        if (string.IsNullOrEmpty(url) || !url.Contains("github.com"))
        {
             // Fallback or show error
             return;
        }
        await MainWindowViewModel.Instance.Factory.Git.ShareOnGitHubAsync(url);
    }


    #region View Menu
    private void UpdateMenuCheckState(string dockableId, MenuItem menuItem)
    {
        try
        {
            if (MainWindowViewModel.Instance?.Layout == null || menuItem == null) return;

            // Check if dockable is in hidden cache
            if (_hiddenDockables.ContainsKey(dockableId))
            {
                menuItem.IsChecked = false;
                return;
            }

            // Find the dockable in the layout
            var result = FindToolDockAndDockable(MainWindowViewModel.Instance.Layout, dockableId);

            if (result.toolDock != null && result.dockable != null)
            {
                // Check if it's in VisibleDockables
                bool isVisible = result.toolDock.VisibleDockables.Contains(result.dockable);
                menuItem.IsChecked = isVisible;
            }
            else
            {
                menuItem.IsChecked = false;
            }
        }
        catch (Exception ex)
        {
            MainWindowViewModel.ReportException(new Exception(ex.Message));
        }
    }
    private void InitializeMenuCheckStates(object? sender, RoutedEventArgs e)
    {
        // Wait a bit for the layout to be fully initialized
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // First, hide unused tabs by default if requested
            HideUnusedTabsByDefault();

            // Find menu items by name
            UpdateMenuCheckState("Solution", this.FindControl<MenuItem>("MenuSolutionExplorer"));
            UpdateMenuCheckState("FileExplorer", this.FindControl<MenuItem>("MenuFileExplorer"));
            UpdateMenuCheckState("Toolbox", this.FindControl<MenuItem>("MenuToolbox"));
            UpdateMenuCheckState("Errors", this.FindControl<MenuItem>("MenuErrorList"));
            UpdateMenuCheckState("BuildOutput", this.FindControl<MenuItem>("MenuOutput"));
            UpdateMenuCheckState("GitChanges", this.FindControl<MenuItem>("MenuGit"));
         
            UpdateMenuCheckState("Terminal", this.FindControl<MenuItem>("MenuTerminal"));
         
            UpdateMenuCheckState("PackageConsole", this.FindControl<MenuItem>("MenuPMC"));
            UpdateMenuCheckState("Properties", this.FindControl<MenuItem>("MenuProperties"));
            UpdateMenuCheckState("Outline", this.FindControl<MenuItem>("MenuOutline"));
            UpdateMenuCheckState("Thumbnail", this.FindControl<MenuItem>("MenuThumbnail"));
            UpdateMenuCheckState("Symbols", this.FindControl<MenuItem>("MenuSymbols"));
            UpdateMenuCheckState("LogicalTree", this.FindControl<MenuItem>("MenuLogicalTree"));
            
            UpdateMenuCheckState("FindReferences", this.FindControl<MenuItem>("MenuReferences"));
            UpdateMenuCheckState("DebugSettings", this.FindControl<MenuItem>("MenuDebugSettings"));
          
            UpdateMenuCheckState("Breakpoints", this.FindControl<MenuItem>("MenuBreakpoints"));
            UpdateMenuCheckState("CallStack", this.FindControl<MenuItem>("MenuCallStack"));
            UpdateMenuCheckState("Locals", this.FindControl<MenuItem>("MenuLocals"));
            UpdateMenuCheckState("Watchers", this.FindControl<MenuItem>("MenuWatchers"));
           
          
            UpdateMenuCheckState("Extensions", this.FindControl<MenuItem>("MenuExtensions"));
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    // View Menu Toggle Methods
    private void ToggleSolutionExplorer_Click(object? sender, RoutedEventArgs e)
    {
        ToggleDockableWithMenu("Solution", sender);
    }

    private void ToggleErrorList_Click(object? sender, RoutedEventArgs e)
    {
        ToggleDockableWithMenu("Errors", sender);
    }

    private void ToggleToolbox_Click(object? sender, RoutedEventArgs e)
    {
        ToggleDockableWithMenu("Toolbox", sender);
    }

    private void ToggleImagePicker_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("Symbols", menuItem);
    }

    private void TogglePageExplorer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("Thumbnail", menuItem);
    }

    private void ToggleProperties_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("Properties", menuItem);
    }

    private void ToggleLogicalTree_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("LogicalTree", menuItem);
    }

    private void ToggleOutput_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("BuildOutput", menuItem);
    }

    

    private void TogglePMC_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("PackageConsole", menuItem);
    }

    private void ToggleOutline_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("Outline", menuItem);
    }

    private void ToggleThumbnail_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("Thumbnail", menuItem);
    }

    private void ToggleSymbols_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("Symbols", menuItem);
    }

   
    private void ToggleGit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("GitChanges", menuItem);
    }

    private void ToggleFileExplorer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("FileExplorer", menuItem);
    }

    

    private void ToggleTerminal_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("Terminal", menuItem);
    }

    private void ToggleVisualTree_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("VisualTree", menuItem);
    }

    private void ToggleReferences_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("FindReferences", menuItem);
    }

    private void ToggleDebugSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("DebugSettings", menuItem);
    }

  
    private void ToggleBreakpoints_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("Breakpoints", menuItem);
    }

    private void ToggleCallStack_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("CallStack", menuItem);
    }

    private void ToggleLocals_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("Locals", menuItem);
    }

    private void ToggleWatchers_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
            ToggleDockableWithMenu("Watchers", menuItem);
    }

    

    

    private void ToggleDockableWithMenu(string dockableId, object? sender)
    {
        try
        {
            var menuItem = sender as MenuItem;
            var button = sender as Button;
            // Get the layout from ViewModel
            if (MainWindowViewModel.Instance?.Layout == null) return;

            // Check if dockable is in hidden cache
            if (_hiddenDockables.ContainsKey(dockableId))
            {
                // Restore from cache
                var cached = _hiddenDockables[dockableId];

                if (cached.toolDock != null && cached.dockable != null)
                {
                    // Add back to VisibleDockables
                    if (!cached.toolDock.VisibleDockables.Contains(cached.dockable))
                    {
                        cached.toolDock.VisibleDockables.Add(cached.dockable);
                    }

                    // Activate it
                    cached.toolDock.ActiveDockable = cached.dockable;

                    // Remove from cache
                    _hiddenDockables.Remove(dockableId);

                    // Update status
                    if (menuItem != null) menuItem.IsChecked = true;
                }
            }
            else
            {
                // Find the ToolDock that contains this dockable
                var result = FindToolDockAndDockable(MainWindowViewModel.Instance.Layout, dockableId);

                if (result.toolDock != null && result.dockable != null)
                {
                    // Check current visibility
                    bool isCurrentlyVisible = result.toolDock.VisibleDockables.Contains(result.dockable);

                    if (isCurrentlyVisible)
                    {
                        // Hide: Store in cache before removing
                        _hiddenDockables[dockableId] = (result.toolDock, result.dockable);

                        // Remove from VisibleDockables
                        result.toolDock.VisibleDockables.Remove(result.dockable);

                        // Activate another dockable if available
                        if (result.toolDock.VisibleDockables.Count > 0)
                        {
                            result.toolDock.ActiveDockable = result.toolDock.VisibleDockables[0];
                        }

                        // Update status
                        if (menuItem != null) menuItem.IsChecked = false;
                    }
                    else
                    {
                        // Show: Add to VisibleDockables (shouldn't happen, but handle it)
                        result.toolDock.VisibleDockables.Add(result.dockable);

                        // Activate it
                        result.toolDock.ActiveDockable = result.dockable;

                        // Update status
                        if (menuItem != null) menuItem.IsChecked = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error silently
            MainWindowViewModel.ReportException(new Exception(ex.Message));
        }
    }

    private (Dock.Model.Core.IDock? toolDock, Dock.Model.Core.IDockable? dockable) FindToolDockAndDockable(Dock.Model.Core.IDockable? root, string id)
    {
        if (root == null) return (null, null);

        // Search in children if this is a dock
        if (root is Dock.Model.Core.IDock dock && dock.VisibleDockables != null)
        {
            // Check direct children
            foreach (var child in dock.VisibleDockables)
            {
                if (child.Id == id)
                {
                    return (dock, child);
                }

                // Recursively search in nested docks
                var result = FindToolDockAndDockable(child, id);
                if (result.dockable != null)
                {
                    return result;
                }
            }
        }

        return (null, null);
    }

    private void HideUnusedTabsByDefault()
    {
        try
        {
            if (MainWindowViewModel.Instance?.Layout == null) return;

            // List of dockable IDs to hide by default
            string[] idsToHide = new[]
            {
                "Git", "PackageManagerConsole",
                "LogicalTree", "VisualTree", "FindReferences", "DebugSettings",
                "LSPSettings", "Breakpoints", "CallStack", "Locals", "Watchers"
                
            };

            foreach (var id in idsToHide)
            {
                // Skip if already in hidden cache
                if (_hiddenDockables.ContainsKey(id)) continue;

                // Find the dockable in the layout
                var result = FindToolDockAndDockable(MainWindowViewModel.Instance.Layout, id);

                if (result.toolDock != null && result.dockable != null)
                {
                    // If it's in the bottom panel (errorsDockPanel), hide it
                    // Check if it's currently visible
                    if (result.toolDock.VisibleDockables.Contains(result.dockable))
                    {
                        // Store in cache
                        _hiddenDockables[id] = (result.toolDock, result.dockable);

                        // Remove from VisibleDockables
                        result.toolDock.VisibleDockables.Remove(result.dockable);
                    }
                }
            }

            // Ensure either Output or Errors is active if they are visible
            var outputResult = FindToolDockAndDockable(MainWindowViewModel.Instance.Layout, "Output");
            if (outputResult.toolDock != null && outputResult.dockable != null &&
                outputResult.toolDock.VisibleDockables.Contains(outputResult.dockable))
            {
                outputResult.toolDock.ActiveDockable = outputResult.dockable;
            }
            else
            {
                var errorsResult = FindToolDockAndDockable(MainWindowViewModel.Instance.Layout, "Errors");
                if (errorsResult.toolDock != null && errorsResult.dockable != null &&
                    errorsResult.toolDock.VisibleDockables.Contains(errorsResult.dockable))
                {
                    errorsResult.toolDock.ActiveDockable = errorsResult.dockable;
                }
            }
        }
        catch (Exception ex)
        {
            MainWindowViewModel.ReportException(ex);
        }
    }


    #endregion
}
