using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Scadix.Designer.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scadix.Designer;

public partial class WelcomeScreen : Window
{
    public static WelcomeScreen? Instance;

    public WelcomeScreen()
    {
        InitializeComponent();
        Instance = this;

        // Allow dragging the custom title bar
        PointerPressed += (_, e) =>
        {
            if (e.GetPosition(this).Y <= 36)
                BeginMoveDrag(e);
        };

        SwitchToProjects();
        CheckMissingTemplates();
    }

    private async void CheckMissingTemplates()
    {
        try
        {
            var packages = await TemplateManager.GetPackagesStatusAsync();
            if (packages.Any(p => !p.IsInstalled))
            {
                TemplateWarningBanner.IsVisible = true;
            }
        }
        catch { }
    }

    private void InstallTemplates_Click(object? sender, RoutedEventArgs e)
    {
        var win = new TemplateInstallerWindow();
        win.ShowDialog(this);
    }

    private void CloseBanner_Click(object? sender, RoutedEventArgs e)
    {
        TemplateWarningBanner.IsVisible = false;
    }

    // ── Window chrome ────────────────────────────────────────────────────────

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseWindow(object? sender, RoutedEventArgs e)
        => Close();

    // ── Navigation ───────────────────────────────────────────────────────────

    public void SwitchToProjects()
    {
        MainContent.Content = new Views.WelcomeProjectsView();
    }

    private void Projects_Click(object? sender, RoutedEventArgs e) => SwitchToProjects();

    private void NewProject_Click(object? sender, RoutedEventArgs e)
    {
        var win = new NewSolutionWindow { OwnerWelcomeScreen = this };
        win.TitleBar.IsVisible = true;
        win.ShowDialog(this);
    }

    private void GetFromVCS_Click(object? sender, RoutedEventArgs e)
    {
        MainContent.Content = new Views.WelcomeCloneView();
    }

    // ── Project actions ──────────────────────────────────────────────────────

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {

        if (StorageProvider is null)
        {
            return;
        }

        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open xaml",
            FileTypeFilter = GetXamlFileTypes(),
            AllowMultiple = false
        });

        var file = result.FirstOrDefault();
        if (file is not null)
        {
            try
            {
                _openXamlFile = file;
                await using var stream = await _openXamlFile.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var fileContent = await reader.ReadToEndAsync();
             
                if (fileContent is { Length: > 0 })
                {
                    MainWindowViewModel.Instance.Open(file.Path.LocalPath);
                    OpenMainWindow();
                }
                reader.Dispose();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
      
    }
    private static List<FilePickerFileType> GetXamlFileTypes()
    {
        return new List<FilePickerFileType>
        {
            StorageService.Axaml,
            StorageService.Xaml,
            StorageService.All
        };
    }
    private IStorageFile? _openXamlFile;
    private async Task OpenXamlFile()
    {
        

        if (StorageProvider is null)
        {
            return;
        }

        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open xaml",
            FileTypeFilter = GetXamlFileTypes(),
            AllowMultiple = false
        });

        var file = result.FirstOrDefault();
        if (file is not null)
        {
            try
            {
                _openXamlFile = file;
                await using var stream = await _openXamlFile.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var fileContent = await reader.ReadToEndAsync();
                
                reader.Dispose();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
    private async void OpenProject_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Solution or Project",
            Filters =
            [
                new FileDialogFilter { Name = "Solution & Project Files", Extensions = ["sln", "slnx", "csproj"] },
                new FileDialogFilter { Name = "All Files", Extensions = ["*"] }
            ]
        };

        var result = await dialog.ShowAsync(this);
        if (result is { Length: > 0 })
        {
            await HandleOpenProject(result[0], false);
        }
    }

    private async void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Open Folder" };
        var result = await dialog.ShowAsync(this);
        if (!string.IsNullOrEmpty(result))
        {
            await HandleOpenProject(result, true);
        }
    }

    private void OpenDesigner_Click(object? sender, RoutedEventArgs e)
    {
        // فتح المصمم بدون مشروع — مباشرة إلى MainWindow
        OpenMainWindow();
    }

    public async Task HandleOpenProject(string path, bool isFolder)
    {
        bool hasCurrent = MainWindowViewModel.Instance.SolutionTree.Count > 0;
        
        if (hasCurrent)
        {
            var options = new Views.ProjectOpenOptionsWindow(hasCurrent);
            await options.ShowDialog(this);

            if (options.Result == Views.ProjectOpenOptionsWindow.OpenResult.Cancel) return;

            if (options.Result == Views.ProjectOpenOptionsWindow.OpenResult.AddToCurrent)
            {
                if (isFolder) MainWindowViewModel.Instance.OpenFolder(path, true);
                else MainWindowViewModel.Instance.OpenSolution(path, true);
                Close();
                return;
            }

            if (options.Result == Views.ProjectOpenOptionsWindow.OpenResult.NewWindow)
            {
                // Launch a new process so each window has its own Shell instance
                var exePath = System.Environment.ProcessPath
                           ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null)
                {
                    Process.Start(new ProcessStartInfo(exePath, $"\"{path}\"") { UseShellExecute = false });
                }
                Close();
                return;
            }
        }

        // First project — load into current Shell and open MainWindow
        if (isFolder) MainWindowViewModel.Instance.OpenFolder(path, false);
        else MainWindowViewModel.Instance.OpenSolution(path, false);
        OpenMainWindow();
    }

    /// <summary>
    /// Show MainWindow and transfer app lifetime to it.
    /// If a MainWindow is already open, reuse it instead of creating a new one.
    /// </summary>
    private MainWindow OpenMainWindow()
    {
        var desktop = Avalonia.Application.Current?.ApplicationLifetime as
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;

        // Reuse existing MainWindow if already open
        var existing = desktop?.Windows.OfType<MainWindow>().FirstOrDefault();
        if (existing != null)
        {
            existing.Activate();
            Close();
            return existing;
        }

        var main = new MainWindow();

        // Transfer the app's main window so closing MainWindow exits the app
        if (desktop != null)
            desktop.MainWindow = main;

        main.Show();
        Close();   // close WelcomeScreen — app lifetime now follows MainWindow
        return main;
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        await settingsWindow.ShowDialog(this);
    }
}

