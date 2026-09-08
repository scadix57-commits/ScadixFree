using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class SystemSettingsPage : UserControl
{
    public SystemSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        ConfirmExitCheckBox.IsChecked = s.ConfirmBeforeExiting;
        ReopenProjectsCheckBox.IsChecked = s.ReopenProjectsOnStartup;
        OpenProjectInComboBox.SelectedIndex = s.OpenProjectInIndex;
        DefaultProjectDirTextBox.Text = s.DefaultProjectDirectory;
        MoveFilesToBinCheckBox.IsChecked = s.MoveFilesToBin;
        SaveWhenSwitchingCheckBox.IsChecked = s.SaveFilesWhenSwitching;
        AutoSaveIdleNumeric.Value = s.AutoSaveIdleSeconds;
        BackupBeforeSavingCheckBox.IsChecked = s.BackupFilesBeforeSaving;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        s.ConfirmBeforeExiting = ConfirmExitCheckBox.IsChecked ?? true;
        s.ReopenProjectsOnStartup = ReopenProjectsCheckBox.IsChecked ?? true;
        s.OpenProjectInIndex = OpenProjectInComboBox.SelectedIndex;
        s.DefaultProjectDirectory = DefaultProjectDirTextBox.Text ?? string.Empty;
        s.MoveFilesToBin = MoveFilesToBinCheckBox.IsChecked ?? true;
        s.SaveFilesWhenSwitching = SaveWhenSwitchingCheckBox.IsChecked ?? true;
        s.AutoSaveIdleSeconds = (int)(AutoSaveIdleNumeric.Value ?? 15);
        s.BackupFilesBeforeSaving = BackupBeforeSavingCheckBox.IsChecked ?? true;
    }
}
