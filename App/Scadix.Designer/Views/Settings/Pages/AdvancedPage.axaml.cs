using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class AdvancedPage : UserControl
{
    public AdvancedPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        ShowMemoryIndicatorCheckBox.IsChecked = s.AdvancedShowMemoryIndicator;
        MaxMemoryNumeric.Value = s.AdvancedMaxMemoryMB;
        ExperimentalFeaturesCheckBox.IsChecked = s.AdvancedEnableExperimentalFeatures;
        EnableLoggingCheckBox.IsChecked = s.AdvancedEnableLogging;
        
        // Set log level
        LogLevelComboBox.SelectedIndex = s.AdvancedLogLevel switch
        {
            "Trace" => 0,
            "Debug" => 1,
            "Info" => 2,
            "Warning" => 3,
            "Error" => 4,
            _ => 2
        };
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        s.AdvancedShowMemoryIndicator = ShowMemoryIndicatorCheckBox.IsChecked ?? false;
        s.AdvancedMaxMemoryMB = (int)(MaxMemoryNumeric.Value ?? 2048);
        s.AdvancedEnableExperimentalFeatures = ExperimentalFeaturesCheckBox.IsChecked ?? false;
        s.AdvancedEnableLogging = EnableLoggingCheckBox.IsChecked ?? false;
        
        s.AdvancedLogLevel = LogLevelComboBox.SelectedIndex switch
        {
            0 => "Trace",
            1 => "Debug",
            2 => "Info",
            3 => "Warning",
            4 => "Error",
            _ => "Info"
        };
    }
}
