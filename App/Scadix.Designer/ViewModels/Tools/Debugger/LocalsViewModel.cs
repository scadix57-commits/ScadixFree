using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Scadix.Designer.Services;
using System.Collections.ObjectModel;

namespace Scadix.Designer.ViewModels.Tools;

/// <summary>
/// Displays local variables when the debugger is paused.
/// Populated by DebugToolbarViewModel.OnStoppedAsync to avoid duplicate StackTrace calls.
/// </summary>
public partial class LocalsViewModel : Tool
{
    public static LocalsViewModel? Current { get; private set; }

    [ObservableProperty]
    private ObservableCollection<VariableViewModel> variables = new();

    public LocalsViewModel()
    {
        Id    = "Locals";
        Title = "Locals";
        Current = this;

        DebuggerService.Instance.DebuggingStopped += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Variables.Clear());
    }

    public void Clear()
    {
        Variables.Clear();
    }
}

public partial class VariableViewModel : ObservableObject
{
    [ObservableProperty] private string name  = "";
    [ObservableProperty] private string value = "";
    [ObservableProperty] private string type  = "";

    public VariableViewModel(string name, string value, string type)
    {
        Name  = name;
        Value = value;
        Type  = type;
    }
}
