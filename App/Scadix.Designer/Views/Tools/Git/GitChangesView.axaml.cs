using Avalonia.Controls;

namespace Scadix.Designer.Views.Tools;

public partial class GitChangesView : UserControl
{
    public GitChangesView()
    {
        InitializeComponent();
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TreeView tree) return;
        if (tree.SelectedItem is not Scadix.Designer.ViewModels.GitChangeNode node) return;
        if (node.IsFolder) return;

        // فتح الـ diff عند تحديد ملف
        if (DataContext is Scadix.Designer.ViewModels.GitChangesViewModel vm)
            vm.OpenFileDiffCommand.Execute(node);
    }
}
