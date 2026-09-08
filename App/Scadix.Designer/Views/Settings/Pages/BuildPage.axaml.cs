using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class BuildPage : UserControl
{
    public BuildPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        SetCheck("ChkBuildBeforeRun",    s.BuildBeforeRun);
        SetCheck("ChkShowOutput",        s.BuildShowOutputAutomatically);
        SetCheck("ChkClearOutput",       s.BuildClearOutputBeforeBuild);
        SetCheck("ChkNullable",          s.BuildEnableNullable);
        SetCheck("ChkImplicitUsings",    s.BuildEnableImplicitUsings);

        var cmbConfig    = this.FindControl<ComboBox>("CmbConfiguration");
        var cmbPlatform  = this.FindControl<ComboBox>("CmbPlatform");
        var txtSdkPath   = this.FindControl<TextBox>("TxtSdkPath");

        if (cmbConfig   != null) cmbConfig.SelectedIndex   = s.BuildConfigurationIndex;
        if (cmbPlatform != null) cmbPlatform.SelectedIndex = s.BuildPlatformIndex;
        if (txtSdkPath  != null) txtSdkPath.Text           = s.DotNetSdkPath;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        s.BuildBeforeRun                = GetCheck("ChkBuildBeforeRun");
        s.BuildShowOutputAutomatically  = GetCheck("ChkShowOutput");
        s.BuildClearOutputBeforeBuild   = GetCheck("ChkClearOutput");
        s.BuildEnableNullable           = GetCheck("ChkNullable");
        s.BuildEnableImplicitUsings     = GetCheck("ChkImplicitUsings");

        var cmbConfig   = this.FindControl<ComboBox>("CmbConfiguration");
        var cmbPlatform = this.FindControl<ComboBox>("CmbPlatform");
        var txtSdkPath  = this.FindControl<TextBox>("TxtSdkPath");

        if (cmbConfig   != null) s.BuildConfigurationIndex = cmbConfig.SelectedIndex;
        if (cmbPlatform != null) s.BuildPlatformIndex      = cmbPlatform.SelectedIndex;
        if (txtSdkPath  != null) s.DotNetSdkPath           = txtSdkPath.Text ?? "";

        // Apply to Shell immediately
        MainWindowViewModel.Instance.Configuration = s.BuildConfigurationIndex == 0 ? "Debug" : "Release";
    }

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
