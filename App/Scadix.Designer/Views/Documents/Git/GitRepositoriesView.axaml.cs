using Avalonia.Controls;
using Scadix.Designer.ViewModels;

namespace Scadix.Designer.Views.Tools;

public partial class GitRepositoriesView : UserControl
{
    public GitRepositoriesView()
    {
        InitializeComponent();
    }

    private void OnCommitSelected(object? sender, SelectionChangedEventArgs e)
    {
        // Adjust row heights: top=*, splitter=Auto, bottom=250 when commit selected
        var grid = this.FindControl<Grid>("PART_OuterGrid") ?? Content as Grid;
        if (grid == null) return;

        var vm = DataContext as GitRepositoriesViewModel;
        bool hasSelection = vm?.SelectedCommit != null;

        if (grid.RowDefinitions.Count >= 3)
        {
            // If no selection, force height to 0 to ensure it's hidden
            grid.RowDefinitions[2] = hasSelection
                ? new RowDefinition(250, GridUnitType.Pixel)
                : new RowDefinition(0, GridUnitType.Pixel);
        }
    }
}
