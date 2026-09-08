using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Scadix.Designer.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace Scadix.Designer.ViewModels.Tools;

public partial class BreakpointsViewModel : Tool
{
    [ObservableProperty]
    private ObservableCollection<BreakpointItemViewModel> breakpoints = new();

    public BreakpointsViewModel()
    {
        Id    = "Breakpoints";
        Title = "Breakpoints";

        BreakpointService.Instance.BreakpointsChanged += (s, e) => UpdateBreakpoints();
        UpdateBreakpoints();
    }

    private void UpdateBreakpoints()
    {
        var allBreakpoints = BreakpointService.Instance.GetAllBreakpoints();

        var newList = allBreakpoints.SelectMany(kvp =>
            kvp.Value.Select(line => new BreakpointItemViewModel(kvp.Key, line))
        ).ToList();

        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            Breakpoints.Clear();
            foreach (var item in newList)
                Breakpoints.Add(item);
        });
    }

    [RelayCommand]
    private void RemoveAll()
    {
        BreakpointService.Instance.ClearAll();
    }
}

public partial class BreakpointItemViewModel : ObservableObject
{
    [ObservableProperty] private string fileName   = "";
    [ObservableProperty] private string filePath   = "";
    [ObservableProperty] private int    lineNumber;

    public BreakpointItemViewModel(string filePath, int lineNumber)
    {
        FilePath   = filePath;
        FileName   = System.IO.Path.GetFileName(filePath);
        LineNumber = lineNumber;
    }
}
