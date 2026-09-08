using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Linq;

namespace Scadix.Designer
{
    public partial class MainScreen : Window
    {
        public MainScreen()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // ── Window chrome handlers ──────────────────────────────────

        private void MinimizeWindow(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseWindow(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        // ── Action handlers ─────────────────────────────────────────

        private void NewProject_Click(object? sender, RoutedEventArgs e)
        {
            // TODO: Implement new project dialog
            // For now, just open the main designer window
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }

        private void Open_Click(object? sender, RoutedEventArgs e)
        {
            // TODO: Implement file open dialog
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }

        private void OpenRecentProject_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string projectName)
            {
                // TODO: Load the specific project
                var mainWindow = new MainWindow();
                mainWindow.Show();
                Close();
            }
        }

        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox searchBox)
                return;

            var searchText = searchBox.Text?.ToLowerInvariant() ?? string.Empty;
            var projectsList = this.FindControl<StackPanel>("ProjectsList");

            if (projectsList == null)
                return;

            // Filter projects based on search text
            foreach (var child in projectsList.Children)
            {
                if (child is Button projectButton)
                {
                    // Search in project name and path
                    var grid = projectButton.Content as Grid;
                    if (grid != null)
                    {
                        var nameBlock = grid.Children
                            .OfType<StackPanel>()
                            .FirstOrDefault()?
                            .Children
                            .OfType<TextBlock>()
                            .FirstOrDefault();

                        var pathBlock = grid.Children
                            .OfType<StackPanel>()
                            .FirstOrDefault()?
                            .Children
                            .OfType<TextBlock>()
                            .Skip(1)
                            .FirstOrDefault();

                        if (nameBlock != null && pathBlock != null)
                        {
                            var name = nameBlock.Text?.ToLowerInvariant() ?? string.Empty;
                            var path = pathBlock.Text?.ToLowerInvariant() ?? string.Empty;

                            projectButton.IsVisible = string.IsNullOrEmpty(searchText) ||
                                                     name.Contains(searchText) ||
                                                     path.Contains(searchText);
                        }
                    }
                }
            }
        }
    }
}
