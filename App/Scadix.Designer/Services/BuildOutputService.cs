using Avalonia.Threading;
using System.Collections.ObjectModel;

namespace Scadix.Designer.Services;

public class BuildOutputService
{
    private static BuildOutputService? _instance;
    public static BuildOutputService Instance => _instance ??= new BuildOutputService();

    public ObservableCollection<string> BuildLogs { get; } = new ObservableCollection<string>();

    private BuildOutputService() { }

    public void AppendLine(string text)
    {
        if (text == null) return;

        if (Dispatcher.UIThread.CheckAccess())
        {
            BuildLogs.Add(text);
        }
        else
        {
            Dispatcher.UIThread.Post(() => BuildLogs.Add(text));
        }
    }

    public void Clear()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            BuildLogs.Clear();
        }
        else
        {
            Dispatcher.UIThread.Post(() => BuildLogs.Clear());
        }
    }
}
