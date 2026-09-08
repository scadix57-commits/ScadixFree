using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class PluginsPage : UserControl
{
    public PluginsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        AutoUpdateCheckBox.IsChecked = s.PluginsAutoUpdate;
        CheckUpdatesOnStartupCheckBox.IsChecked = s.PluginsCheckForUpdatesOnStartup;
        CustomRepositoryTextBox.Text = s.PluginsCustomRepositoryUrl;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        s.PluginsAutoUpdate = AutoUpdateCheckBox.IsChecked ?? true;
        s.PluginsCheckForUpdatesOnStartup = CheckUpdatesOnStartupCheckBox.IsChecked ?? true;
        s.PluginsCustomRepositoryUrl = CustomRepositoryTextBox.Text ?? string.Empty;
    }
}
