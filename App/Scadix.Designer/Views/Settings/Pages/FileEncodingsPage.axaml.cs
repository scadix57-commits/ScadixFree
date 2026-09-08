using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class FileEncodingsPage : UserControl
{
    public FileEncodingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        SetComboIndex("CmbGlobalEncoding", s.FileEncodingGlobalIndex);
        SetComboIndex("CmbProjectEncoding", s.FileEncodingProjectIndex);
        SetComboIndex("CmbUtf8BomMode", s.FileEncodingUtf8BomModeIndex);
        SetCheck("ChkTransparentNativeToAscii", s.FileEncodingTransparentNativeToAscii);
        SetCheck("ChkAutoDetectEncoding", s.FileEncodingAutoDetect);
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        s.FileEncodingGlobalIndex = GetComboIndex("CmbGlobalEncoding");
        s.FileEncodingProjectIndex = GetComboIndex("CmbProjectEncoding");
        s.FileEncodingUtf8BomModeIndex = GetComboIndex("CmbUtf8BomMode");
        s.FileEncodingTransparentNativeToAscii = GetCheck("ChkTransparentNativeToAscii");
        s.FileEncodingAutoDetect = GetCheck("ChkAutoDetectEncoding");
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
    private void SetComboIndex(string name, int index)
    {
        var c = this.FindControl<ComboBox>(name);
        if (c != null) c.SelectedIndex = index;
    }
    private int GetComboIndex(string name)
    {
        var c = this.FindControl<ComboBox>(name);
        return c?.SelectedIndex ?? 0;
    }
}
