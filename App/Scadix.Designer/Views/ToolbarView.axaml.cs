using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Scadix.Designer.Services;
using Scadix.Designer.ViewModels.Tools;
using Scadix.Designer.Views;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scadix.Designer;

public partial class ToolbarView : UserControl
{
    // ── الـ DebuggerService الجديد (DAP مباشر عبر stdin/stdout) ──────────
    private readonly DebuggerService _dbg = DebuggerService.Instance;
    private readonly DebugToolbarViewModel _debugVm = DebugToolbarViewModel.Instance;

    public ToolbarView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;

        // ربط حالة الأزرار بـ DebugToolbarViewModel
        _debugVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DebugToolbarViewModel.IsDebugging)
                               or nameof(DebugToolbarViewModel.IsPaused))
                UpdateDebuggerButtons(_debugVm.IsDebugging, _debugVm.IsPaused);
        };

        // Initialize Branch Popup Flyout
        var host = this.FindControl<ContentControl>("BranchFlyoutHost");
        if (host != null)
        {
            var popup = new BranchPopup(MainWindowViewModel.Instance.Factory.Git);
            popup.RequestClose += () =>
            {
                var branchBtn = this.FindControl<Button>("BranchButton");
                branchBtn?.Flyout?.Hide();
            };
            host.Content = popup;

            var branchBtn = this.FindControl<Button>("BranchButton");
            if (branchBtn?.Flyout is Avalonia.Controls.Flyout f)
            {
                f.Opened += (_, _) => popup.RefreshBranchList();
            }
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        DataContext = this.FindAncestorOfType<Window>()?.DataContext;
    }

    private void UpdateDebuggerButtons(bool isDebugging, bool isPaused)
    {
        var btnStartDebug   = this.FindControl<Button>("BtnStartDebug");
        var btnStopDebug    = this.FindControl<Button>("BtnStopDebug");
        var btnRestartDebug = this.FindControl<Button>("BtnRestartDebug");
        var btnContinue     = this.FindControl<Button>("BtnContinue");
        var btnPause        = this.FindControl<Button>("BtnPause");
        var btnStepOver     = this.FindControl<Button>("BtnStepOver");
        var btnStepInto     = this.FindControl<Button>("BtnStepInto");
        var btnStepOut      = this.FindControl<Button>("BtnStepOut");
        var sep1            = this.FindControl<Control>("SepDebug1");
        var sep2            = this.FindControl<Control>("SepDebug2");

        if (btnStartDebug   != null) btnStartDebug.IsEnabled   = !isDebugging;
        
        if (btnStopDebug    != null) { btnStopDebug.IsEnabled = isDebugging; btnStopDebug.IsVisible = isDebugging; }
        if (btnRestartDebug != null) { btnRestartDebug.IsEnabled = isDebugging; btnRestartDebug.IsVisible = isDebugging; }
        if (sep1            != null) sep1.IsVisible = isDebugging;

        if (btnContinue     != null) { btnContinue.IsEnabled = isDebugging && isPaused; btnContinue.IsVisible = isDebugging; }
        if (btnPause        != null) { btnPause.IsEnabled = isDebugging && !isPaused; btnPause.IsVisible = isDebugging; }
        if (btnStepOver     != null) { btnStepOver.IsEnabled = isDebugging && isPaused; btnStepOver.IsVisible = isDebugging; }
        if (btnStepInto     != null) { btnStepInto.IsEnabled = isDebugging && isPaused; btnStepInto.IsVisible = isDebugging; }
        if (btnStepOut      != null) { btnStepOut.IsEnabled = isDebugging && isPaused; btnStepOut.IsVisible = isDebugging; }
        
        if (sep2            != null) sep2.IsVisible = isDebugging;
    }

    // ── Debugger Button Handlers ─────────────────────────────────────────

    private async void StartDebug_Click(object? sender, RoutedEventArgs e)
    {
        // تحقق من إعدادات الـ debugger — كما في النسخة القديمة
        var settings = Scadix.Designer.Settings.Default;
        if (!settings.DebuggerUseNetCoreDbg)
        {
            await ShowDebuggerNotConfiguredDialog();
            return;
        }

        var shell = MainWindowViewModel.Instance;
        var startupProject = shell.SelectedStartupProject;

        if (startupProject == null)
        {
            await ShowMessageAsync("No Startup Project",
                "Please select a startup project from the toolbar.");
            return;
        }

        var executablePath = GetExecutablePath(startupProject);
        if (string.IsNullOrEmpty(executablePath))
        {
            await ShowMessageAsync("Build Required",
                "Please build the project first before debugging.");
            return;
        }

        var projectPath = System.IO.Path.GetDirectoryName(startupProject.FilePath) ?? "";
        await _dbg.StartDebuggingAsync(projectPath, executablePath);
    }

    private void StopDebug_Click(object? sender, RoutedEventArgs e)
        => _dbg.StopDebugging();

    private async void RestartDebug_Click(object? sender, RoutedEventArgs e)
    {
        _dbg.StopDebugging();
        await Task.Delay(500);

        var settings = Scadix.Designer.Settings.Default;
        if (!settings.DebuggerUseNetCoreDbg) return;

        var shell = MainWindowViewModel.Instance;
        var startupProject = shell.SelectedStartupProject;
        if (startupProject == null) return;

        var executablePath = GetExecutablePath(startupProject);
        if (!string.IsNullOrEmpty(executablePath))
        {
            var projectPath = System.IO.Path.GetDirectoryName(startupProject.FilePath) ?? "";
            await _dbg.StartDebuggingAsync(projectPath, executablePath);
        }
    }

    private async void Continue_Click(object? sender, RoutedEventArgs e)
        => await _dbg.ContinueAsync();

    private async void Pause_Click(object? sender, RoutedEventArgs e)
        => await _dbg.PauseAsync();

    private async void StepOver_Click(object? sender, RoutedEventArgs e)
        => await _dbg.StepOverAsync();

    private async void StepInto_Click(object? sender, RoutedEventArgs e)
        => await _dbg.StepInAsync();

    private async void StepOut_Click(object? sender, RoutedEventArgs e)
        => await _dbg.StepOutAsync();

    private async void AttachToProcess_Click(object? sender, RoutedEventArgs e)
    {
        var settings = Scadix.Designer.Settings.Default;
        if (!settings.DebuggerUseNetCoreDbg)
        {
            await ShowDebuggerNotConfiguredDialog();
            return;
        }

        var dialog = new AttachToProcessDialog();
        var parentWindow = this.FindAncestorOfType<Window>();

        if (parentWindow != null)
        {
            var result = await dialog.ShowDialog<int?>(parentWindow);
            if (result.HasValue && result.Value > 0)
            {
                BuildOutputService.Instance.AppendLine(
                    $"[Debugger] Attach to process {result.Value} — not yet implemented via DAP.");
            }
        }
    }

    // ── Helper Methods ───────────────────────────────────────────────────

    private string? GetExecutablePath(SolutionNode startupProject)
    {
        var shell = MainWindowViewModel.Instance;
        var config = shell.Configuration ?? "Debug";
        var projectPath = startupProject.FilePath;

        if (!string.IsNullOrEmpty(projectPath))
        {
            var projectDir  = System.IO.Path.GetDirectoryName(projectPath);
            var projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath);

            var possiblePaths = new[]
            {
                System.IO.Path.Combine(projectDir!, "bin", config, "net10.0", $"{projectName}.dll"),
                System.IO.Path.Combine(projectDir!, "bin", config, "net10.0",  $"{projectName}.dll"),
                System.IO.Path.Combine(projectDir!, "bin", config, "net10.0",  $"{projectName}.dll"),
                System.IO.Path.Combine(projectDir!, "bin", config, "net7.0",  $"{projectName}.dll"),
                System.IO.Path.Combine(projectDir!, "bin", config, "net6.0",  $"{projectName}.dll"),
                System.IO.Path.Combine(projectDir!, "bin", config,            $"{projectName}.dll"),
            };

            return possiblePaths.FirstOrDefault(System.IO.File.Exists);
        }

        return null;
    }

    private async Task ShowDebuggerNotConfiguredDialog()
    {
        await ShowMessageAsync("Debugger Not Configured",
            "netcoredbg debugger is not enabled or configured.\n\n" +
            "Please go to Settings > Build, Execution, Deployment > Debugger and:\n" +
            "1. Enable 'Use netcoredbg'\n" +
            "2. Set the path to netcoredbg executable\n" +
            "3. Configure debugger options");
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var parentWindow = this.FindAncestorOfType<Window>();
        if (parentWindow == null) return;

        var dialog = new Window
        {
            Title = title,
            Width = 450,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(25),
                Spacing = 20,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 15,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "ℹ",
                                FontSize = 32,
                                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2196F3")),
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
                            },
                            new TextBlock
                            {
                                Text = message,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                FontSize = 13,
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                MaxWidth = 350
                            }
                        }
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Width = 100,
                        Height = 32,
                        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2196F3")),
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.White),
                        CornerRadius = new Avalonia.CornerRadius(4)
                    }
                }
            }
        };

        var okButton = (dialog.Content as StackPanel)?.Children[1] as Button;
        if (okButton != null) okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(parentWindow);
    }

    // ── Existing Methods ─────────────────────────────────────────────────

    private void Home_Click(object? sender, RoutedEventArgs e)
    {
        var welcome = new WelcomeScreen();
        welcome.Show();
    }

    private void OpenSolution_Click(object? sender, RoutedEventArgs e)
        => MainWindowViewModel.Instance.OpenSolutionDialog();

    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
        => MainWindowViewModel.Instance.OpenFolderDialog();



    // ── Page Dimension Controls (toolbar) ────────────────────────────────────

    /// <summary>
    /// Called when the user presses Enter in the Width or Height textbox.
    /// </summary>
    private void PageDimension_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ApplyPageDimensions();
    }

    /// <summary>
    /// Called when the Width or Height textbox loses focus.
    /// </summary>
    private void PageDimension_LostFocus(object? sender, RoutedEventArgs e)
    {
        ApplyPageDimensions();
    }

    /// <summary>
    /// Called when the user picks a device preset from the combo.
    /// </summary>
    private void PagePreset_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var combo = sender as ComboBox;
        if (combo?.SelectedItem is not ComboBoxItem item) return;

        var tag = item.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        var parts = tag.Split(',');
        if (parts.Length != 2) return;

        var widthBox = this.FindControl<TextBox>("PageWidthBox");
        var heightBox = this.FindControl<TextBox>("PageHeightBox");
        if (widthBox != null) widthBox.Text = parts[0];
        if (heightBox != null) heightBox.Text = parts[1];

        ApplyPageDimensions();


    }

    /// <summary>
    /// Reads Width/Height boxes and patches d:DesignWidth / d:DesignHeight
    /// (and optionally Width/Height) in the current document's XAML text.
    /// </summary>
    private void ApplyPageDimensions()
    {
        try
        {
            var widthBox = this.FindControl<TextBox>("PageWidthBox");
            var heightBox = this.FindControl<TextBox>("PageHeightBox");

            if (!int.TryParse(widthBox?.Text, out int w) || w <= 0) return;
            if (!int.TryParse(heightBox?.Text, out int h) || h <= 0) return;

            var doc = MainWindowViewModel.Instance.CurrentDocument;
            if (doc == null || string.IsNullOrEmpty(doc.Text)) return;

            var xaml = doc.Text;

            // تطبيق الأبعاد على الصفحة المفتوحة حالياً
            var designSurface = MainWindowViewModel.Instance.CurrentDocument?.DesignSurface;
            if (designSurface?.DesignContext?.RootItem != null)
            {
                var root = designSurface.DesignContext.RootItem;
                root.Properties.GetProperty(Avalonia.Controls.Control.WidthProperty).SetValue((double)w);
                root.Properties.GetProperty(Avalonia.Controls.Control.HeightProperty).SetValue((double)h);
            }


            // Persist to settings
             Settings.Default.LastPageWidth = w;
             Settings.Default.LastPageHeight = h;
             Settings.Default.Save();
        }
        catch (Exception ex)
        {
            MainWindowViewModel.ReportException(ex);
        }
    }



    /// <summary>
    /// Reads d:DesignWidth / d:DesignHeight from the current document and
    /// populates the toolbar textboxes.  Called whenever the active document changes.
    /// </summary>
    private void SyncPageDimensionBoxes()
    {
        try
        {
            var widthBox = this.FindControl<TextBox>("PageWidthBox");
            var heightBox = this.FindControl<TextBox>("PageHeightBox");
            if (widthBox == null || heightBox == null) return;

            var xaml = MainWindowViewModel.Instance.CurrentDocument?.Text ?? string.Empty;

            var wMatch = Regex.Match(xaml, @"d:DesignWidth\s*=\s*""([^""]+)""");
            var hMatch = Regex.Match(xaml, @"d:DesignHeight\s*=\s*""([^""]+)""");

            widthBox.Text = wMatch.Success ? wMatch.Groups[1].Value : string.Empty;
            heightBox.Text = hMatch.Success ? hMatch.Groups[1].Value : string.Empty;
        }
        catch { /* non-critical */ }
    }

}
