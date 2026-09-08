using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class KeymapPage : UserControl
{
    public KeymapPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        var cmbKeymapScheme = this.FindControl<ComboBox>("CmbKeymapScheme");
        if (cmbKeymapScheme != null) cmbKeymapScheme.SelectedIndex = s.KeymapSchemeIndex;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        var cmbKeymapScheme = this.FindControl<ComboBox>("CmbKeymapScheme");
        if (cmbKeymapScheme != null) s.KeymapSchemeIndex = cmbKeymapScheme.SelectedIndex;
    }
}
