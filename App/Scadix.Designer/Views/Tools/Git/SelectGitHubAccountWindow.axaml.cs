using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Scadix.Designer.ViewModels;

namespace Scadix.Designer.Views.Tools.Git;

public partial class SelectGitHubAccountWindow : Window
{
    public SelectGitHubAccountWindow()
    {
        InitializeComponent();
    }

    public SelectGitHubAccountWindow(SelectGitHubAccountViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () => Close();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
