using Avalonia.Controls;
using Scadix.Designer.ViewModels;

namespace Scadix.Designer.Views.Tools.Git;

public partial class NewBranchWindow : Window
{
    public NewBranchWindow()
    {
        InitializeComponent();
    }

    public NewBranchWindow(NewBranchViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
