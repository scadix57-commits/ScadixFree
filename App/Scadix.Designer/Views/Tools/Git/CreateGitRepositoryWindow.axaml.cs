using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Scadix.Designer.ViewModels;

namespace Scadix.Designer.Views.Tools.Git;

public partial class CreateGitRepositoryWindow : Window
{
    public CreateGitRepositoryWindow()
    {
        InitializeComponent();
     }

    public CreateGitRepositoryWindow(CreateGitRepositoryViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += () => Close();
    }

   
}
