using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class CompilerPage : UserControl
{
    public CompilerPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        SetText("TxtHeapSize", s.CompilerHeapSizeMB.ToString());
        SetCheck("ChkParallelCompilation", s.CompilerParallelCompilation);
        SetText("TxtOutputPath", s.CompilerOutputPath);
        SetText("TxtAdditionalArgs", s.CompilerAdditionalArgs);
        SetCheck("ChkTreatWarningsAsErrors", s.CompilerTreatWarningsAsErrors);
        SetComboIndex("CmbWarningLevel", s.CompilerWarningLevelIndex);
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        if (int.TryParse(GetText("TxtHeapSize"), out var hs)) s.CompilerHeapSizeMB = hs;
        s.CompilerParallelCompilation = GetCheck("ChkParallelCompilation");
        s.CompilerOutputPath = GetText("TxtOutputPath");
        s.CompilerAdditionalArgs = GetText("TxtAdditionalArgs");
        s.CompilerTreatWarningsAsErrors = GetCheck("ChkTreatWarningsAsErrors");
        s.CompilerWarningLevelIndex = GetComboIndex("CmbWarningLevel");
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
    private void SetText(string name, string value)
    {
        var t = this.FindControl<TextBox>(name);
        if (t != null) t.Text = value;
    }
    private string GetText(string name)
    {
        var t = this.FindControl<TextBox>(name);
        return t?.Text ?? "";
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
