using Scadix.Designer.Services;
using Dock.Model.Mvvm.Controls;
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace Scadix.Designer.ViewModels.Tools;

public class BuildOutputToolViewModel : Tool
{
    public BuildOutputToolViewModel()
    {
        Id    = "BuildOutput";
        Title = "Build Output";
    }

    public ObservableCollection<string> Logs => BuildOutputService.Instance.BuildLogs;


    public void AppendLine(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Logs.Add(text);
        });
    }

    public void Clear()
    {
        Dispatcher.UIThread.Post(() => Logs.Clear());
    }
}
