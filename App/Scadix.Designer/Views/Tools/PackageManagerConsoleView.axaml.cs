using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Scadix.Designer.ViewModels.Tools;
using System;

namespace Scadix.Designer.Views.Tools;

public partial class PackageManagerConsoleView : UserControl
{
    private TextBox?      _inputBox;
    private ScrollViewer? _scroll;

    public PackageManagerConsoleView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is PackageManagerConsoleViewModel vm)
            vm.Output.CollectionChanged += (_, _) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(ScrollToBottom,
                    Avalonia.Threading.DispatcherPriority.Background);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _inputBox = this.FindNameScope()?.Find<TextBox>("InputBox");
        _scroll   = this.FindNameScope()?.Find<ScrollViewer>("OutputScroll");

        _inputBox?.AddHandler(KeyDownEvent, OnInputKey, RoutingStrategies.Tunnel);
    }

    private void OnInputKey(object? sender, KeyEventArgs e)
    {
        var vm = DataContext as PackageManagerConsoleViewModel;
        if (vm == null) return;

        switch (e.Key)
        {
            case Key.Enter:
                vm.RunInputCommand.Execute(null);
                ScrollToBottom();
                e.Handled = true;
                break;
            case Key.Up:
                vm.HistoryUpCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Down:
                vm.HistoryDownCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void ScrollToBottom() => _scroll?.ScrollToEnd();
}
