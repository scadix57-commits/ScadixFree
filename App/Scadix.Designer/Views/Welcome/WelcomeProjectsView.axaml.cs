using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Linq;

namespace Scadix.Designer.Views;

public partial class WelcomeProjectsView : UserControl
{
    public WelcomeProjectsView()
    {
        InitializeComponent();
    }

    private async void OpenRecentProject_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            bool isFolder = System.IO.Directory.Exists(path) && (ext != ".sln" && ext != ".csproj");
            await WelcomeScreen.Instance?.HandleOpenProject(path, isFolder);
        }
    }

    // ── Context menu handlers ─────────────────────────────────────────────────

    private async void ContextOpen_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            bool isFolder = System.IO.Directory.Exists(path) && (ext != ".sln" && ext != ".csproj");
            await WelcomeScreen.Instance?.HandleOpenProject(path, isFolder);
        }
    }

    private void ContextRemove_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is RecentProjectEntry entry)
        {
            MainWindowViewModel.Instance.RecentProjects.Remove(entry);
            RecentProjectsStore.Save();
        }
    }

    private void ContextOpenExplorer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string path)
        {
            var dir = System.IO.File.Exists(path)
                ? System.IO.Path.GetDirectoryName(path)
                : path;
            if (dir != null && System.IO.Directory.Exists(dir))
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"")
                    { UseShellExecute = true });
        }
    }
    private void ClearAll_Click(object? sender, RoutedEventArgs e)
    {
        MainWindowViewModel.Instance.RecentProjects.Clear();
        RecentProjectsStore.Save();
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var query = tb.Text?.Trim() ?? string.Empty;

        // Filter RecentProjects collection directly
        // (ItemsControl doesn't have Children — we filter the source)
        var filtered = string.IsNullOrEmpty(query)
            ? MainWindowViewModel.Instance.RecentProjects
            : new System.Collections.ObjectModel.ObservableCollection<RecentProjectEntry>(
                MainWindowViewModel.Instance.RecentProjects.Where(
                    r => r.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                         r.Path.Contains(query, System.StringComparison.OrdinalIgnoreCase)));

        var list = this.FindControl<Avalonia.Controls.ItemsControl>("ProjectsList");
        if (list != null)
            list.ItemsSource = filtered;
    }
}
