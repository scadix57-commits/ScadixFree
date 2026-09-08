using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scadix.Designer.ViewModels;

namespace Scadix.Designer.ViewModels;

public partial class CreateGitRepositoryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _localPath = "";

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private string _licenseTemplate = "None";

    [ObservableProperty]
    private string _account = "scadix57-commits (GitHub)";

    [ObservableProperty]
    private string _owner = "scadix57-commits";

    [ObservableProperty]
    private string _repositoryName = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private string _visibility = "Private";

    [ObservableProperty]
    private string _remoteUrl = "";

    [ObservableProperty]
    private bool _isLocalOnly = false;

    [ObservableProperty]
    private string _statusMessage = "";

    public ObservableCollection<string> LicenseTemplates { get; } = new() { "None", "MIT", "Apache 2.0", "GPL v3" };
    public ObservableCollection<string> Accounts { get; } = new() { "scadix57-commits (GitHub)" };
    public ObservableCollection<string> Owners { get; } = new() { "scadix57-commits" };
    public ObservableCollection<string> Visibilities { get; } = new() { "Public", "Private" };

    public event Action? RequestClose;

    public CreateGitRepositoryViewModel(string localPath)
    {
        LocalPath = localPath;
        RepositoryName = Path.GetFileName(localPath) ?? "NewRepository";
        UpdateRemoteUrl();
    }

    partial void OnOwnerChanged(string value) => UpdateRemoteUrl();
    partial void OnRepositoryNameChanged(string value) => UpdateRemoteUrl();

    private void UpdateRemoteUrl()
    {
        RemoteUrl = $"https://github.com/{Owner}/{RepositoryName}";
    }

    [RelayCommand]
    private void SetLocalOnly()
    {
        IsLocalOnly = true;
    }

    [RelayCommand]
    private void SetGitHub()
    {
        IsLocalOnly = false;
        // Optionally set other remote flags if needed
    }

    [RelayCommand]
    private void SelectAccount()
    {
        var vm = new SelectGitHubAccountViewModel();
        var win = new Scadix.Designer.Views.Tools.Git.SelectGitHubAccountWindow(vm);
        
        vm.AccountSelected += (acc) => {
            Account = acc.Name + " (GitHub)";
            Owner = acc.Name;
        };

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            win.ShowDialog(desktop.MainWindow);
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = IsLocalOnly ? "Initializing local repository..." : "Creating and pushing to GitHub...";
            
            if (!Directory.Exists(LocalPath)) Directory.CreateDirectory(LocalPath);

            // Initialize git locally
            StatusMessage = "Running git init...";
            // Stage and commit initial files so 'master' branch is created
            StatusMessage = "Staging files...";
            await RunGitCommandAsync(LocalPath, "add .");
            
            StatusMessage = "Committing files...";
            // We use a try-catch for commit because if there are no files, git commit returns 1
            try { await RunGitCommandAsync(LocalPath, "commit -m \"Initial commit\""); } catch { }

            if (!IsLocalOnly)
            {
                StatusMessage = "Adding remote origin...";
                await RunGitCommandAsync(LocalPath, $"remote add origin {RemoteUrl}");
                
                StatusMessage = "Pushing to GitHub...";
                await RunGitCommandAsync(LocalPath, "push -u origin master");
            }

            StatusMessage = "Refreshing workspace...";
            MainWindowViewModel.Instance.Factory.Git.RefreshRepositories();
            
            StatusMessage = "Success!";
            await Task.Delay(1000); // Let the user see the success message
            
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = "Error: " + ex.Message;
            MainWindowViewModel.ReportException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunGitCommandAsync(string workingDir, string arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}
