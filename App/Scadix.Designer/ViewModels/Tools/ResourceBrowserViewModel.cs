using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scadix.AxamlDom;
using Scadix.Designer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Scadix.Designer.ViewModels.Tools
{
    public partial class ResourceBrowserViewModel : ObservableObject
    {
        // ── Observable Properties ─────────────────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<ResourceGroup> groups = new();

        [ObservableProperty]
        private ObservableCollection<ResourceItem> filteredItems = new();

        [ObservableProperty]
        private string searchQuery = string.Empty;

        [ObservableProperty]
        private string selectedCategory = "All";

        [ObservableProperty]
        private ResourceItem? selectedItem;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string statusText = "Loading resources...";

        [ObservableProperty]
        private int totalCount;

        // ── Categories ────────────────────────────────────────────────────────────

        public ObservableCollection<string> Categories { get; } = new()
        {
            "All", "🎨 Colors", "🖌 Brushes", "✨ Styles",
            "🎛 Control Themes", "🖼️ Images", "📝 Strings", "📏 Numbers", "📦 Other"
        };

        // ── Constructor ───────────────────────────────────────────────────────────

        public ResourceBrowserViewModel()
        {
            PropertyChanged += OnSelfChanged;

            // ✅ Listen for project load/open events and refresh automatically
          
            MainWindowViewModel.Instance.ProjectOpened += OnProjectPathChanged;

            // ✅ Load immediately if a project is already open
            var currentPath = DesignerProjectContext.CurrentProjectPath;
            if (!string.IsNullOrEmpty(currentPath))
            {
                _ = LoadResourcesAsync(currentPath);
            }
        }

        // ── Project Change Handler ─────────────────────────────────────────────

        private void OnProjectPathChanged(string projectPath)
        {
            _ = LoadResourcesAsync(projectPath);
        }

        // ── Commands ──────────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task RefreshAsync()
        {
            var projectRoot = DesignerProjectContext.CurrentProjectPath;
            await LoadResourcesAsync(projectRoot);
        }

        [RelayCommand]
        private void SelectCategory(string category)
        {
            SelectedCategory = category ?? "All";
        }

        [RelayCommand]
        private async Task CopyXamlRef()
        {
            if (SelectedItem == null) return;
            try
            {
                var clipboard = TopLevelHelper.GetClipboard();
                if (clipboard != null)
                    await clipboard.SetTextAsync(SelectedItem.XamlRef);
                StatusText = $"Copied: {SelectedItem.XamlRef}";
            }
            catch (Exception ex) { StatusText = $"Copy failed: {ex.Message}"; }
        }

        [RelayCommand]
        private async Task CopyKey()
        {
            if (SelectedItem == null) return;
            try
            {
                var clipboard = TopLevelHelper.GetClipboard();
                if (clipboard != null)
                    await clipboard.SetTextAsync(SelectedItem.Key);
                StatusText = $"Copied key: {SelectedItem.Key}";
            }
            catch (Exception ex) { StatusText = $"Copy failed: {ex.Message}"; }
        }

        // ── Core Load ─────────────────────────────────────────────────────────────

        private async Task LoadResourcesAsync(string? projectRoot)
        {
            IsLoading = true;
            StatusText = "Scanning resources...";

            try
            {
                var allGroups = await ResourceBrowserService.ScanAsync(projectRoot);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Groups.Clear();
                    foreach (var g in allGroups)
                        Groups.Add(g);

                    // Reset filter when project changes
                    SearchQuery = string.Empty;
                    SelectedCategory = "All";
                    ApplyFilter();
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Filter Logic ──────────────────────────────────────────────────────────

        private void OnSelfChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SearchQuery) or nameof(SelectedCategory))
                ApplyFilter();
        }

        private void ApplyFilter()
        {
            var query = SearchQuery?.Trim() ?? string.Empty;
            var cat   = SelectedCategory ?? "All";

            var allItems = Groups.SelectMany(g => g.Items).ToList();

            IEnumerable<ResourceItem> result = cat == "All"
                ? allItems
                : allItems.Where(i => GroupNameForKind(i.Kind) == cat);

            if (!string.IsNullOrEmpty(query))
                result = result.Where(i =>
                    i.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.Source.Contains(query, StringComparison.OrdinalIgnoreCase));

            FilteredItems.Clear();
            foreach (var item in result.Take(500))
                FilteredItems.Add(item);

            TotalCount = FilteredItems.Count;
            StatusText = TotalCount > 0
                ? $"{TotalCount} resource(s) found"
                : "No resources found";
        }

        private static string GroupNameForKind(ResourceKind kind) => kind switch
        {
            ResourceKind.Color        => "🎨 Colors",
            ResourceKind.Brush        => "🖌 Brushes",
            ResourceKind.Style        => "✨ Styles",
            ResourceKind.ControlTheme => "🎛 Control Themes",
            ResourceKind.Image        => "🖼️ Images",
            ResourceKind.String       => "📝 Strings",
            ResourceKind.Number       => "📏 Numbers",
            _                         => "📦 Other"
        };
    }

    /// <summary>Helper to get Clipboard from the active top-level window.</summary>
    internal static class TopLevelHelper
    {
        public static Avalonia.Input.Platform.IClipboard? GetClipboard()
        {
            try
            {
                var topLevel = Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                return topLevel is not null
                    ? Avalonia.Controls.TopLevel.GetTopLevel(topLevel)?.Clipboard
                    : null;
            }
            catch { return null; }
        }
    }
}
