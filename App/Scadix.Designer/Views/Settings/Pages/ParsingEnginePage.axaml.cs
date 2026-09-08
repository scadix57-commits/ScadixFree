using Avalonia.Controls;
using Scadix.AxamlDom;

namespace Scadix.Designer.Views.Settings;

public partial class ParsingEnginePage : UserControl
{
    public ParsingEnginePage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        var combo = this.FindControl<ComboBox>("EngineCombo");
        if (combo != null)
        {
            combo.SelectedIndex = s.SelectedParsingEngine switch
            {
                XamlParsingEngine.Automatic => 0,
                XamlParsingEngine.Avalonia  => 1,
                XamlParsingEngine.Manual    => 2,
                XamlParsingEngine.AXSG      => 3,
                _                           => 0
            };
        }

        var chkDeep    = this.FindControl<CheckBox>("ChkDeepResolution");
        var chkPreload = this.FindControl<CheckBox>("ChkBackgroundPreload");
        var chkReload  = this.FindControl<CheckBox>("ChkReloadAfterChange");

        if (chkDeep    != null) chkDeep.IsChecked    = s.DeepResourceResolution;
        if (chkPreload != null) chkPreload.IsChecked = s.BackgroundAssemblyPreload;
        if (chkReload  != null) chkReload.IsChecked  = s.ReloadDesignerAfterEngineChange;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        var combo = this.FindControl<ComboBox>("EngineCombo");
        if (combo != null)
        {
            s.SelectedParsingEngine = combo.SelectedIndex switch
            {
                0 => XamlParsingEngine.Automatic,
                1 => XamlParsingEngine.Avalonia,
                2 => XamlParsingEngine.Manual,
                3 => XamlParsingEngine.AXSG,
                _ => XamlParsingEngine.Automatic
            };
        }

        var chkDeep    = this.FindControl<CheckBox>("ChkDeepResolution");
        var chkPreload = this.FindControl<CheckBox>("ChkBackgroundPreload");
        var chkReload  = this.FindControl<CheckBox>("ChkReloadAfterChange");

        if (chkDeep    != null) s.DeepResourceResolution           = chkDeep.IsChecked    == true;
        if (chkPreload != null) s.BackgroundAssemblyPreload        = chkPreload.IsChecked == true;
        if (chkReload  != null) s.ReloadDesignerAfterEngineChange  = chkReload.IsChecked  == true;
    }
}
