using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class NuGetPage : UserControl
{
    public NuGetPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        IncludePrereleaseCheckBox.IsChecked = s.NuGetIncludePrerelease;
        IncludeUnlistedCheckBox.IsChecked = s.NuGetIncludeUnlisted;
        AutoRestoreMissingCheckBox.IsChecked = s.NuGetAutoRestoreMissingPackages;
        AutoRestoreBeforeBuildCheckBox.IsChecked = s.NuGetAutoRestoreBeforeBuild;
        DependencyBehaviorComboBox.SelectedIndex = s.NuGetDependencyBehaviorIndex;
        PackageFormatComboBox.SelectedIndex = s.NuGetPackageFormatIndex;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        s.NuGetIncludePrerelease = IncludePrereleaseCheckBox.IsChecked ?? false;
        s.NuGetIncludeUnlisted = IncludeUnlistedCheckBox.IsChecked ?? false;
        s.NuGetAutoRestoreMissingPackages = AutoRestoreMissingCheckBox.IsChecked ?? true;
        s.NuGetAutoRestoreBeforeBuild = AutoRestoreBeforeBuildCheckBox.IsChecked ?? true;
        s.NuGetDependencyBehaviorIndex = DependencyBehaviorComboBox.SelectedIndex;
        s.NuGetPackageFormatIndex = PackageFormatComboBox.SelectedIndex;
    }
}
