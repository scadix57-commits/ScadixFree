using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Scadix.Designer.ViewModels;

public class GitHubAccountInfo : ObservableObject
{
    public string Name { get; set; } = "";
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public partial class SelectGitHubAccountViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private GitHubAccountInfo? _selectedAccount;

    public ObservableCollection<GitHubAccountInfo> Accounts { get; } = new()
    {
        new GitHubAccountInfo { Name = "AbdalaMask" },
        new GitHubAccountInfo { Name = "scadix57-commits" }
    };

    public event Action<GitHubAccountInfo>? AccountSelected;
    public event Action? RequestClose;

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue()
    {
        if (SelectedAccount != null)
        {
            AccountSelected?.Invoke(SelectedAccount);
            RequestClose?.Invoke();
        }
    }

    private bool CanContinue() => SelectedAccount != null;

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void AddAccount()
    {
        // logic to add new account
    }
}
