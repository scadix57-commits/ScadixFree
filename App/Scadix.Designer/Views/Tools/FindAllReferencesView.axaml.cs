using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Scadix.Designer.ViewModels.Tools;

namespace Scadix.Designer.Views.Tools;

public partial class FindAllReferencesView : UserControl
{
    public FindAllReferencesView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is FindAllReferencesViewModel vm)
                vm.NavigateRequested += OnNavigateRequested;
        };
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var queryBox = this.FindNameScope()?.Find<TextBox>("QueryBox");
        queryBox?.AddHandler(KeyDownEvent, OnQueryKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is FindAllReferencesViewModel vm)
        {
            vm.RunSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnNavigateRequested(string filePath, int line, int column)
    {
        // Navigation is handled by MainWindowViewModel.Open
        // Line/column navigation can be added once the editor supports it
    }
}
