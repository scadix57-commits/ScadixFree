using Avalonia;
using Avalonia.Controls;
using System.Linq;

namespace Scadix.Designer.Views.Settings;

public partial class AppearancePage : UserControl
{
    public AppearancePage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;

        // Theme
        var themeCombo = this.FindControl<ComboBox>("ThemeCombo");
        if (themeCombo != null) themeCombo.SelectedIndex = s.ThemeIndex;

        var syncOs = this.FindControl<CheckBox>("SyncOsTheme");
        if (syncOs != null) syncOs.IsChecked = s.SyncWithOsTheme;

        // UI Font
        var uiFontCombo = this.FindControl<ComboBox>("UiFontCombo");
        if (uiFontCombo != null)
        {
            // Find matching font or default to Segoe UI
            var items = uiFontCombo.Items.Cast<ComboBoxItem>().Select(i => i.Content?.ToString() ?? "").ToList();
            var idx = items.IndexOf(s.UiFont);
            uiFontCombo.SelectedIndex = idx >= 0 ? idx : 0;
        }

        var uiFontSizeCombo = this.FindControl<ComboBox>("UiFontSizeCombo");
        if (uiFontSizeCombo != null) uiFontSizeCombo.SelectedIndex = s.UiFontSizeIndex;

        // UI Elements
        SetCheck("ChkStatusBar", s.ShowStatusBar);
        SetCheck("ChkMainToolbar", s.ShowMainToolbar);
        SetCheck("ChkToolWindowBars", s.ShowToolWindowBars);

        // Tab Placement
        var tabPlacementCombo = this.FindControl<ComboBox>("TabPlacementCombo");
        if (tabPlacementCombo != null) tabPlacementCombo.SelectedIndex = s.TabPlacementIndex;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;

        // Theme
        var themeCombo = this.FindControl<ComboBox>("ThemeCombo");
        if (themeCombo != null)
        {
            s.ThemeIndex = themeCombo.SelectedIndex;
            ApplyTheme(themeCombo.SelectedIndex);
        }

        var syncOs = this.FindControl<CheckBox>("SyncOsTheme");
        if (syncOs != null) s.SyncWithOsTheme = syncOs.IsChecked == true;

        // UI Font
        var uiFontCombo = this.FindControl<ComboBox>("UiFontCombo");
        if (uiFontCombo != null && uiFontCombo.SelectedItem is ComboBoxItem item)
            s.UiFont = item.Content?.ToString() ?? "Segoe UI";

        var uiFontSizeCombo = this.FindControl<ComboBox>("UiFontSizeCombo");
        if (uiFontSizeCombo != null) s.UiFontSizeIndex = uiFontSizeCombo.SelectedIndex;

        // UI Elements
        s.ShowStatusBar = GetCheck("ChkStatusBar");
        s.ShowMainToolbar = GetCheck("ChkMainToolbar");
        s.ShowToolWindowBars = GetCheck("ChkToolWindowBars");

        // Tab Placement
        var tabPlacementCombo = this.FindControl<ComboBox>("TabPlacementCombo");
        if (tabPlacementCombo != null) s.TabPlacementIndex = tabPlacementCombo.SelectedIndex;

        ApplyToUI();
    }

    private static void ApplyTheme(int index)
    {
        if (Application.Current == null) return;
        Application.Current.RequestedThemeVariant = index switch
        {
            0 => Avalonia.Styling.ThemeVariant.Dark,
            1 => Avalonia.Styling.ThemeVariant.Light,
            _ => Avalonia.Styling.ThemeVariant.Default
        };
    }

    private static void ApplyToUI()
    {
        var s = Scadix.Designer.Settings.Default;
        var mainWindow = MainWindow.Instance;

        if (mainWindow == null) return;

        // Apply UI visibility
        var statusBar = mainWindow.StatusBar;
        if (statusBar != null) statusBar.IsVisible = s.ShowStatusBar;

        var toolbar = mainWindow.MainToolbar;
        if (toolbar != null) toolbar.IsVisible = s.ShowMainToolbar;
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private void SetCheck(string name, bool value)
    {
        var c = this.FindControl<CheckBox>(name);
        if (c != null) c.IsChecked = value;
    }
    private bool GetCheck(string name)
    {
        var c = this.FindControl<CheckBox>(name);
        return c?.IsChecked == true;
    }
}
