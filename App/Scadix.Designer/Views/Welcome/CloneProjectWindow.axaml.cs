using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scadix.Designer.Views;

public partial class CloneProjectWindow : Window
{
    public string? ResultPath { get; private set; }
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            this.DataContext = this; // Simple binding trigger
        }
    }

    public CloneProjectWindow()
    {
        InitializeComponent();
        ClonePath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "XAMLStudioProjects");
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        var result = await dialog.ShowAsync(this);
        if (result != null)
        {
            ClonePath.Text = result;
        }
    }

    private async void Clone_Click(object? sender, RoutedEventArgs e)
    {
        var url = RepoUrl.Text?.Trim();
        var path = ClonePath.Text?.Trim();

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(path)) return;

        IsBusy = true;
        try
        {
            // Extract repo name from URL
            var repoName = url.Split('/').Last().Replace(".git", "");
            var fullPath = Path.Combine(path, repoName);

            await Task.Run(async () =>
            {
                var si = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone \"{url}\" \"{fullPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var p = Process.Start(si);
                if (p != null) await p.WaitForExitAsync();
            });

            // Find project or solution in cloned dir
            var sln = Directory.GetFiles(fullPath, "*.sln").FirstOrDefault();
            var csproj = Directory.GetFiles(fullPath, "*.csproj").FirstOrDefault();

            ResultPath = sln ?? csproj ?? fullPath;
            Close();
        }
        catch (Exception ex)
        {
            // Simple error handling
            Debug.WriteLine(ex.Message);
        }
        finally { IsBusy = false; }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
