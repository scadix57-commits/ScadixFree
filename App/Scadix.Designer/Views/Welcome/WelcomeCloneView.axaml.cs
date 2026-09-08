using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scadix.Designer.Views;

public partial class WelcomeCloneView : UserControl
{
    private string _defaultParentDir;

    public WelcomeCloneView()
    {
        InitializeComponent();

        _defaultParentDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "XAMLStudioProjects");

        RepoUrl.TextChanged += RepoUrl_TextChanged;
        CloneBtn.Click += CloneBtn_Click;
        CancelBtn.Click += (s, e) => WelcomeScreen.Instance?.SwitchToProjects();
    }

    private void RepoUrl_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var url = RepoUrl.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(url)) return;

        // Extract project name from URL (e.g. https://github.com/user/repo.git -> repo)
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            var lastPart = parts.Last();
            if (lastPart.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                lastPart = lastPart.Substring(0, lastPart.Length - 4);

            ClonePath.Text = Path.Combine(_defaultParentDir, lastPart);
        }
    }

    private async void CloneBtn_Click(object? sender, RoutedEventArgs e)
    {
        var url = RepoUrl.Text?.Trim();
        var path = ClonePath.Text?.Trim();

        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(path)) return;

        GitProgress.IsVisible = true;
        CloneBtn.IsEnabled = false;

        bool success = await Task.Run(() => RunGitClone(url, path));

        GitProgress.IsVisible = false;
        CloneBtn.IsEnabled = true;

        if (success)
        {
            // Search for solution or project file
            var slnPath = Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories).FirstOrDefault()
                       ?? Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();

            if (slnPath != null && WelcomeScreen.Instance != null)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await WelcomeScreen.Instance.HandleOpenProject(slnPath, false);
                });
            }
        }
    }

    private bool RunGitClone(string url, string path)
    {
        try
        {
            // Ensure parent directory exists
            var parent = Path.GetDirectoryName(path);
            if (parent != null) Directory.CreateDirectory(parent);

            var psi = new ProcessStartInfo("git", $"clone \"{url}\" \"{path}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var proc = Process.Start(psi);
            proc?.WaitForExit(120_000); // 2 min timeout
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }
}
