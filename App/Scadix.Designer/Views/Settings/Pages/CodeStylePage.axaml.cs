using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class CodeStylePage : UserControl
{
    public CodeStylePage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        SetComboIndex("CmbScheme", s.CodeStyleSchemeIndex);
        SetText("TxtTabSize", s.CodeStyleTabSize.ToString());
        SetText("TxtIndentSize", s.CodeStyleIndentSize.ToString());
        SetText("TxtContinuationIndent", s.CodeStyleContinuationIndent.ToString());
        SetCheck("ChkKeepIndentsOnEmptyLines", s.CodeStyleKeepIndentsOnEmptyLines);
        SetCheck("ChkSpaceBeforeMethodParentheses", s.CodeStyleSpaceBeforeMethodParentheses);
        SetCheck("ChkSpaceBeforeMethodCallParentheses", s.CodeStyleSpaceBeforeMethodCallParentheses);
        SetCheck("ChkSpaceWithinMethodParentheses", s.CodeStyleSpaceWithinMethodParentheses);
        SetCheck("ChkSpaceWithinBrackets", s.CodeStyleSpaceWithinBrackets);
        SetText("TxtHardWrapAt", s.CodeStyleHardWrapAt.ToString());
        SetCheck("ChkWrapLongLines", s.CodeStyleWrapOnTyping);
        SetComboIndex("CmbBracesPlacement", s.CodeStyleBracesPlacementIndex);
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        s.CodeStyleSchemeIndex = GetComboIndex("CmbScheme");
        if (int.TryParse(GetText("TxtTabSize"), out var ts)) s.CodeStyleTabSize = ts;
        if (int.TryParse(GetText("TxtIndentSize"), out var ind)) s.CodeStyleIndentSize = ind;
        if (int.TryParse(GetText("TxtContinuationIndent"), out var ci)) s.CodeStyleContinuationIndent = ci;
        s.CodeStyleKeepIndentsOnEmptyLines = GetCheck("ChkKeepIndentsOnEmptyLines");
        s.CodeStyleSpaceBeforeMethodParentheses = GetCheck("ChkSpaceBeforeMethodParentheses");
        s.CodeStyleSpaceBeforeMethodCallParentheses = GetCheck("ChkSpaceBeforeMethodCallParentheses");
        s.CodeStyleSpaceWithinMethodParentheses = GetCheck("ChkSpaceWithinMethodParentheses");
        s.CodeStyleSpaceWithinBrackets = GetCheck("ChkSpaceWithinBrackets");
        if (int.TryParse(GetText("TxtHardWrapAt"), out var hw)) s.CodeStyleHardWrapAt = hw;
        s.CodeStyleWrapOnTyping = GetCheck("ChkWrapLongLines");
        s.CodeStyleBracesPlacementIndex = GetComboIndex("CmbBracesPlacement");
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
