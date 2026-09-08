using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Scadix.Designer.Views;

public class ProjectOpenOptionsViewModel
{
    public bool CanAdd { get; set; }
}

public partial class ProjectOpenOptionsWindow : Window
{
    public enum OpenResult { Cancel, NewWindow, AddToCurrent }
    public OpenResult Result { get; private set; } = OpenResult.Cancel;

    public ProjectOpenOptionsWindow(bool canAdd)
    {
        InitializeComponent();
        DataContext = new ProjectOpenOptionsViewModel { CanAdd = canAdd };
    }

    private void NewWindow_Click(object? sender, RoutedEventArgs e)
    {
        Result = OpenResult.NewWindow;
        Close();
    }

    private void AddToCurrent_Click(object? sender, RoutedEventArgs e)
    {
        Result = OpenResult.AddToCurrent;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = OpenResult.Cancel;
        Close();
    }
}
