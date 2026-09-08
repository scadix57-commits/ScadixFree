using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Scadix.Designer.ViewModels;
using System;
using System.Linq;

namespace Scadix.Designer.Views;

public partial class BranchPopup : UserControl
{
    public event Action? RequestClose;

    private readonly GitRepositoriesViewModel _git;
    private string _searchText = "";

    public BranchPopup(GitRepositoriesViewModel git)
    {
        _git = git;
        DataContext = git;
        InitializeComponent();

        RefreshBranchList();
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text ?? "";
        RefreshBranchList();
    }

    // ── Branch list ───────────────────────────────────────────────────────────

    public void RefreshBranchList()
    {
        BranchList.Children.Clear();

        var query = _searchText.ToLowerInvariant();

        // ── Local ──────────────────────────────────────────────────────────
        var localFiltered = _git.LocalBranches
            .Where(b => string.IsNullOrEmpty(query) || b.Name.ToLowerInvariant().Contains(query))
            .ToList();

        if (localFiltered.Count > 0)
        {
            BranchList.Children.Add(MakeSectionHeader("Local", "▾"));
            foreach (var branch in localFiltered)
                BranchList.Children.Add(MakeBranchItem(branch, isRemote: false));
        }

        // ── Remote ─────────────────────────────────────────────────────────
        var remoteFiltered = _git.RemoteBranches
            .Where(b => string.IsNullOrEmpty(query) || b.Name.ToLowerInvariant().Contains(query))
            .ToList();

        if (remoteFiltered.Count > 0)
        {
            BranchList.Children.Add(MakeSectionHeader("Remote", "▸"));
            foreach (var branch in remoteFiltered)
                BranchList.Children.Add(MakeBranchItem(branch, isRemote: true));
        }
    }

    private Border MakeSectionHeader(string title, string arrow)
    {
        var panel = new DockPanel { Margin = new Thickness(8, 8, 8, 2) };
        panel.Children.Add(new TextBlock
        {
            Text = arrow + "  " + title,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.Gray,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });
        return new Border { Child = panel };
    }

    private Button MakeBranchItem(GitBranch branch, bool isRemote)
    {
        var btn = new Button { Classes = { "branch-item" } };

        // Content: icon + name + tracking + arrow
        var panel = new DockPanel();

        // Branch icon
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M13,8.5C13,9.7 12.3,10.8 11.3,11.3V12.7C12.3,13.2 13,14.3 13,15.5C13,17.4 11.4,19 9.5,19C7.6,19 6,17.4 6,15.5C6,14.3 6.7,13.2 7.7,12.7V11.3C6.7,10.8 6,9.7 6,8.5C6,6.6 7.6,5 9.5,5C11.4,5 13,6.6 13,8.5Z"),
            Fill = branch.IsCurrent ? Brushes.DodgerBlue : Brushes.Gray,
            Width = 9, Height = 13,
            Stretch = Stretch.Uniform,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        DockPanel.SetDock(icon, Avalonia.Controls.Dock.Left);
        panel.Children.Add(icon);

        // Arrow button → submenu (styled as clickable)
        var arrowBtn = new Button
        {
            Content = "›",
            FontSize = 14,
            Foreground = Brushes.Gray,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        ToolTip.SetTip(arrowBtn, "More actions...");
        DockPanel.SetDock(arrowBtn, Avalonia.Controls.Dock.Right);
        panel.Children.Add(arrowBtn);

        // Branch name
        panel.Children.Add(new TextBlock
        {
            Text = branch.Name,
            FontSize = 12,
            FontWeight = branch.IsCurrent ? FontWeight.Bold : FontWeight.Normal,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });

        btn.Content = panel;

        // Single click = Checkout directly (like Rider)
        btn.Click += async (_, _) =>
        {
            if (!branch.IsCurrent)
            {
                RequestClose?.Invoke();
                await _git.CheckoutBranchAsync(branch);
            }
        };

        // Arrow button = show submenu
        arrowBtn.Click += (_, e) =>
        {
            ShowBranchSubmenu(btn, branch, isRemote);
        };

        return btn;
    }

    private void ShowBranchSubmenu(Button anchor, GitBranch branch, bool isRemote)
    {
        var menu = new ContextMenu();

        if (!isRemote)
        {
            if (!branch.IsCurrent)
                menu.Items.Add(MakeItem("Checkout", () => { _ = _git.CheckoutBranchAsync(branch); RequestClose?.Invoke(); }));

            menu.Items.Add(MakeItem($"New Branch from '{branch.Name}'...",
                () => { ShowNewBranchFromDialog(branch.Name); }));

            if (!branch.IsCurrent)
            {
                menu.Items.Add(MakeItem($"Checkout and Rebase onto '{_git.CurrentBranch}'",
                    () => { _ = _git.RebaseBranchAsync(branch); RequestClose?.Invoke(); }));
                menu.Items.Add(MakeItem("Checkout and Update",
                    () => { _ = _git.CheckoutBranchAsync(branch); RequestClose?.Invoke(); }));
            }

            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem($"Compare with '{_git.CurrentBranch}'",
                () => { _ = _git.RefreshAsync(); RequestClose?.Invoke(); }));
            menu.Items.Add(MakeItem("Show Diff with Working Tree",
                () => { _ = _git.RefreshAsync(); RequestClose?.Invoke(); }));
            menu.Items.Add(new Separator());

            if (!branch.IsCurrent)
            {
                menu.Items.Add(MakeItem($"Rebase '{_git.CurrentBranch}' onto '{branch.Name}'",
                    () => { _ = _git.RebaseBranchAsync(branch); RequestClose?.Invoke(); }));
                menu.Items.Add(MakeItem($"Merge '{branch.Name}' into '{_git.CurrentBranch}'",
                    () => { _ = _git.MergeBranchAsync(branch); RequestClose?.Invoke(); }));
                menu.Items.Add(new Separator());
            }

            menu.Items.Add(MakeItem("Push...",   () => { _ = _git.PreparePushAsync(); RequestClose?.Invoke(); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Rename...", () => ShowRenameBranchDialog(branch)));
            menu.Items.Add(MakeItem("Delete",    () => { _ = _git.DeleteBranchAsync(branch); RequestClose?.Invoke(); },
                foreground: Brushes.IndianRed));
        }
        else
        {
            menu.Items.Add(MakeItem("Checkout as Local Branch",
                () => { _ = _git.CheckoutBranchAsync(branch); RequestClose?.Invoke(); }));
            menu.Items.Add(MakeItem($"New Branch from '{branch.Name}'...",
                () => ShowNewBranchFromDialog(branch.Name)));
        }

        menu.Open(anchor);
    }

    private static MenuItem MakeItem(string header, Action action,
        IBrush? foreground = null)
    {
        var item = new MenuItem { Header = header };
        if (foreground != null) item.Foreground = foreground;
        item.Click += (_, _) => action();
        return item;
    }

    // ── Top action handlers ───────────────────────────────────────────────────

    private void Update_Click(object? sender, RoutedEventArgs e)
    {
        _ = _git.PullAsync();
        RequestClose?.Invoke();
    }

    private void Commit_Click(object? sender, RoutedEventArgs e)
    {
        MainWindowViewModel.Instance.Factory.Commit.RefreshAsync();
        RequestClose?.Invoke();
    }

    private void Push_Click(object? sender, RoutedEventArgs e)
    {
        _ = _git.PreparePushAsync();
        RequestClose?.Invoke();
    }

    private void NewBranch_Click(object? sender, RoutedEventArgs e)
        => ShowNewBranchFromDialog(_git.CurrentBranch);

    private void CheckoutTag_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: checkout tag/revision dialog
        RequestClose?.Invoke();
    }

    // ── Dialogs ───────────────────────────────────────────────────────────────

    private async void ShowNewBranchFromDialog(string fromBranch)
    {
        var win = new Window
        {
            Title = $"New Branch from '{fromBranch}'",
            Width = 380, Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var tb = new TextBox
        {
            Watermark = "Branch name...",
            Margin = new Thickness(16, 16, 16, 4)
        };
        var hint = new TextBlock
        {
            Text = $"From: {fromBranch}",
            FontSize = 10,
            Foreground = Brushes.Gray,
            Margin = new Thickness(16, 0, 16, 8)
        };
        var btn = new Button
        {
            Content = "Create & Checkout",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 16, 16)
        };
        btn.Classes.Add("accent");

        btn.Click += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(tb.Text))
            {
                await _git.CreateBranchFromAsync(fromBranch, tb.Text.Trim());
                win.Close();
                RequestClose?.Invoke();
            }
        };

        win.Content = new StackPanel { Children = { tb, hint, btn } };
        
        var owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
        if (owner != null)
            await win.ShowDialog(owner);
        else
            win.Show();
    }

    private async void ShowRenameBranchDialog(GitBranch branch)
    {
        var win = new Window
        {
            Title = $"Rename '{branch.Name}'",
            Width = 360, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var tb = new TextBox
        {
            Text = branch.Name,
            Margin = new Thickness(16, 16, 16, 8)
        };
        var btn = new Button
        {
            Content = "Rename",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 16, 16)
        };
        btn.Classes.Add("accent");

        btn.Click += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(tb.Text) && tb.Text != branch.Name)
            {
                await _git.RenameBranchAsync(branch, tb.Text.Trim());
                win.Close();
                RequestClose?.Invoke();
            }
        };

        win.Content = new StackPanel { Children = { tb, btn } };
        
        var owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
        if (owner != null)
            await win.ShowDialog(owner);
        else
            win.Show();
    }
}
