using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Scadix.Designer.Services;
using System.Collections.ObjectModel;

namespace Scadix.Designer.ViewModels.Tools;

/// <summary>
/// Displays the call stack when the debugger is paused.
/// Populated by DebugToolbarViewModel.OnStoppedAsync to avoid duplicate StackTrace calls.
/// </summary>
public partial class CallStackViewModel : Tool
{
    public static CallStackViewModel? Current { get; private set; }

    [ObservableProperty]
    private ObservableCollection<StackFrameViewModel> frames = new();

    public CallStackViewModel()
    {
        Id    = "CallStack";
        Title = "Call Stack";
        Current = this;

        DebuggerService.Instance.DebuggingStopped += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Frames.Clear());
    }

    public void Clear()
    {
        Frames.Clear();
    }
}

public partial class StackFrameViewModel : ObservableObject
{
    [ObservableProperty] private string name           = "";
    [ObservableProperty] private string sourceLocation = "";

    public StackFrameViewModel(string name, string sourceLocation)
    {
        Name           = name;
        SourceLocation = sourceLocation;
    }
}
