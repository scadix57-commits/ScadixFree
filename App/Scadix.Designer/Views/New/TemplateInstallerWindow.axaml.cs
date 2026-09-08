using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace Scadix.Designer;

// ── Helper models ─────────────────────────────────────────────────────────────

public class InstalledTemplateItem
{
    public string Name      { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string Language  { get; set; } = "";
    public string Tags      { get; set; } = "";
    public string Category  { get; set; } = "";
    public string Icon => Category switch
    {
        "HMI"          => "✨",
        "Uno Platform" => "🟣",
        "Avalonia UI"  => "🔷",
        "Desktop"      => "🖥",
        "Web"          => "🌐",
        "Console"      => "⬛",
        "Library"      => "📦",
        "MAUI"         => "📱",
        "Test"         => "🧪",
        "Services"     => "⚙",
        _              => "📄"
    };
}

public class PopularPackageItem
{
    public string Name        { get; set; } = "";
    public string PackageId   { get; set; } = "";
    public string Description { get; set; } = "";
}

// ── Window ────────────────────────────────────────────────────────────────────

public partial class TemplateInstallerWindow : Window
{
    private readonly ObservableCollection<TemplatePackage>      _packages  = new();
    private readonly ObservableCollection<InstalledTemplateItem> _allTemplates    = new();
    private readonly ObservableCollection<InstalledTemplateItem> _filteredTemplates = new();

    private static readonly List<PopularPackageItem> _popularPackages = new()
    {
        new() { Name = "Uno Platform",          PackageId = "Uno.Templates",                                    Description = "Official Uno Platform cross-platform templates." },
        new() { Name = "Avalonia UI",            PackageId = "Avalonia.Templates",                               Description = "Official Avalonia UI desktop templates." },
        new() { Name = "MAUI",                   PackageId = "Microsoft.Maui.Templates.net10",                   Description = ".NET MAUI cross-platform mobile/desktop templates." },
        new() { Name = "Blazor / ASP.NET Core",  PackageId = "Microsoft.DotNet.Web.ProjectTemplates.10.0",       Description = "ASP.NET Core, Blazor, and Web API templates." },
        new() { Name = "Common .NET Templates",  PackageId = "Microsoft.DotNet.Common.ProjectTemplates.10.0",    Description = "Console, Class Library, and other common templates." },
        new() { Name = "WPF Templates",          PackageId = "Microsoft.DotNet.Wpf.ProjectTemplates.net10",      Description = "WPF application and library templates." },
        new() { Name = "WinForms Templates",     PackageId = "Microsoft.DotNet.WinForms.ProjectTemplates.net10", Description = "Windows Forms application templates." },
        new() { Name = "NUnit Test",             PackageId = "NUnit3.DotNetNew.Template",                        Description = "NUnit 3 test project templates." },
        new() { Name = "xUnit Test",             PackageId = "xunit.v3.templates",                               Description = "xUnit v3 test project templates." },
    };

    public TemplateInstallerWindow()
    {
        InitializeComponent();

        PackageList.ItemsSource   = _packages;
        InstalledList.ItemsSource = _filteredTemplates;
        PopularList.ItemsSource   = _popularPackages;

        Loaded += async (_, _) =>
        {
            await RefreshPackageStatus();
            await LoadInstalledTemplates();
        };
    }

    // ── Tab 1: Essential Packages ─────────────────────────────────────────────

    private async Task RefreshPackageStatus()
    {
        var status = await TemplateManager.GetPackagesStatusAsync();
        _packages.Clear();
        foreach (var p in status) _packages.Add(p);
        UpdateInstallAllButton();
    }

    private void UpdateInstallAllButton()
    {
        InstallAllBtn.IsEnabled = _packages.Any(p => !p.IsInstalled);
    }

    private async void Install_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TemplatePackage pkg)
            await InstallPackage(pkg);
    }

    private async void Uninstall_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TemplatePackage pkg)
            await UninstallPackage(pkg.PackageId, onDone: async () =>
            {
                pkg.Status = "Missing";
                RefreshCollection(_packages);
                UpdateInstallAllButton();
                await LoadInstalledTemplates();
            });
    }

    private async Task InstallPackage(TemplatePackage pkg)
    {
        pkg.Status = "Installing";
        RefreshCollection(_packages);

        bool ok = await TemplateManager.InstallPackageAsync(pkg.PackageId);
        pkg.Status = ok ? "Installed" : "Error";
        RefreshCollection(_packages);
        UpdateInstallAllButton();

        if (ok) await LoadInstalledTemplates();
    }

    private async void InstallAll_Click(object? sender, RoutedEventArgs e)
    {
        InstallAllBtn.IsEnabled = false;
        foreach (var pkg in _packages.Where(p => !p.IsInstalled).ToList())
            await InstallPackage(pkg);
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
        => await RefreshPackageStatus();

    // ── Tab 2: Installed Templates ────────────────────────────────────────────

    private async Task LoadInstalledTemplates()
    {
        TemplateCountLabel.Text = "Loading…";

        var categories = await Task.Run(() => TemplateCatalog.Load(forceRefresh: true));
        var items = categories
            .SelectMany(c => c.Templates.Select(t => new InstalledTemplateItem
            {
                Name      = t.Name,
                ShortName = t.ShortName,
                Language  = t.Language,
                Tags      = t.Tags,
                Category  = t.Category
            }))
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToList();

        _allTemplates.Clear();
        foreach (var item in items) _allTemplates.Add(item);

        ApplySearch(SearchBox?.Text ?? "");
    }

    private void ApplySearch(string query)
    {
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allTemplates.ToList()
            : _allTemplates.Where(t =>
                t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        _filteredTemplates.Clear();
        foreach (var item in filtered) _filteredTemplates.Add(item);

        TemplateCountLabel.Text = $"{_filteredTemplates.Count} template(s) found";
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        => ApplySearch(SearchBox.Text ?? "");

    private async void UninstallTemplate_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not InstalledTemplateItem item) return;

        // Confirm
        var confirm = new Window
        {
            Title  = "Confirm Uninstall",
            Width  = 380, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin  = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Uninstall template \"{item.Name}\" ({item.ShortName})?",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 13
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children =
                        {
                            new Button { Content = "Uninstall", Tag = "yes",
                                Background = new SolidColorBrush(Color.Parse("#D32F2F")),
                                Foreground = new SolidColorBrush(Colors.White),
                                Padding = new Avalonia.Thickness(14,6), CornerRadius = new Avalonia.CornerRadius(4) },
                            new Button { Content = "Cancel", Tag = "no",
                                Padding = new Avalonia.Thickness(14,6), CornerRadius = new Avalonia.CornerRadius(4) }
                        }
                    }
                }
            }
        };

        string result = "no";
        var btns = ((confirm.Content as StackPanel)!.Children[1] as StackPanel)!.Children.OfType<Button>().ToList();
        foreach (var b in btns) b.Click += (_, _) => { result = b.Tag?.ToString() ?? "no"; confirm.Close(); };

        await confirm.ShowDialog(this);
        if (result != "yes") return;

        await UninstallPackage(item.ShortName, onDone: async () =>
        {
            await LoadInstalledTemplates();
            await RefreshPackageStatus();
        });
    }

    private async void ReloadTemplates_Click(object? sender, RoutedEventArgs e)
        => await LoadInstalledTemplates();

    // ── Tab 3: Add Custom Package ─────────────────────────────────────────────

    private async void AddPackage_Click(object? sender, RoutedEventArgs e)
    {
        var id = PackageIdBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(id)) return;

        await RunInstall(id);
    }

    private async void InstallPopular_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PopularPackageItem item)
            await RunInstall(item.PackageId);
    }

    private async Task RunInstall(string packageId)
    {
        AddPackageBtn.IsEnabled = false;
        ShowAddStatus($"Installing {packageId}…", "#0078D4");

        bool ok = await TemplateManager.InstallPackageAsync(packageId);

        if (ok)
        {
            ShowAddStatus($"✔ '{packageId}' installed successfully.", "#2E7D32");
            PackageIdBox.Text = "";
            await LoadInstalledTemplates();
            await RefreshPackageStatus();
        }
        else
        {
            ShowAddStatus($"✘ Failed to install '{packageId}'. Check the package ID and your internet connection.", "#D32F2F");
        }

        AddPackageBtn.IsEnabled = true;
    }

    private void ShowAddStatus(string msg, string color)
    {
        AddStatusLabel.Text       = msg;
        AddStatusLabel.Foreground = new SolidColorBrush(Color.Parse(color));
        AddStatusLabel.IsVisible  = true;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static async Task UninstallPackage(string packageIdOrShortName, Func<Task> onDone)
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", $"new uninstall {packageIdOrShortName}")
            {
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };
            using var proc = Process.Start(psi);
            if (proc != null) await proc.WaitForExitAsync();
        }
        catch { /* ignore */ }

        await onDone();
    }

    /// <summary>Force ObservableCollection to re-render by replacing items in-place.</summary>
    private static void RefreshCollection<T>(ObservableCollection<T> col)
    {
        var list = col.ToList();
        col.Clear();
        foreach (var item in list) col.Add(item);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
