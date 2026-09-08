using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Scadix.Designer.ViewModels.Tools;
using System;

namespace Scadix.Designer.Views.Tools;

public partial class TerminalView : UserControl
{
    private ScrollViewer? _outputScroll;
    private TextBox?       _inputBox;
    private TerminalViewModel? _vm;

    public TerminalView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is TerminalViewModel vm)
        {
            _vm = vm;

            // Auto-open a session the first time the panel becomes visible.
            vm.PropertyChanged += (_, pe) =>
            {
                if (pe.PropertyName == nameof(TerminalViewModel.ActiveSession))
                    BindActiveSession();
            };
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _outputScroll = this.FindNameScope()?.Find<ScrollViewer>("OutputScroll");
        _inputBox     = this.FindNameScope()?.Find<TextBox>("InputBox");

        _inputBox?.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_vm != null)
            await _vm.EnsureActiveSessionAsync();
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        var session = _vm?.ActiveSession;
        if (session == null) return;

        switch (e.Key)
        {
            case Key.Enter:
                session.SendInputCommand.Execute(null);
                ScrollToBottom();
                e.Handled = true;
                break;

            case Key.Up:
                session.HistoryUpCommand.Execute(null);
                MoveCaretToEnd();
                e.Handled = true;
                break;

            case Key.Down:
                session.HistoryDownCommand.Execute(null);
                MoveCaretToEnd();
                e.Handled = true;
                break;

            case Key.L when e.KeyModifiers == KeyModifiers.Control:
                session.ClearScreenCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // ── Auto-scroll ───────────────────────────────────────────────────────────

    private void BindActiveSession()
    {
        var session = _vm?.ActiveSession;
        if (session == null) return;

        // Scroll to bottom whenever a new output line is added.
        session.Output.CollectionChanged += (_, _) =>
            Dispatcher.UIThread.Post(ScrollToBottom, DispatcherPriority.Background);

        _inputBox?.Focus();
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        _outputScroll?.ScrollToEnd();
    }

    private void MoveCaretToEnd()
    {
        if (_inputBox != null)
            _inputBox.CaretIndex = _inputBox.Text?.Length ?? 0;
    }
}
