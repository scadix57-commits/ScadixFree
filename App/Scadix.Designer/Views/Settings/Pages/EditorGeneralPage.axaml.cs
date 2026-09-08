using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class EditorGeneralPage : UserControl
{
    public EditorGeneralPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        SetCheck("ChkLineNumbers",    s.EditorShowLineNumbers);
        SetCheck("ChkIndentGuides",   s.EditorShowIndentGuides);
        SetCheck("ChkHighlightLine",  s.EditorHighlightCurrentLine);
        SetCheck("ChkMatchBraces",    s.EditorShowMatchingBraces);
        SetCheck("ChkWhitespace",     s.EditorShowWhitespace);
        SetCheck("ChkAutoSave",       s.EditorAutoSave);
        SetCheck("ChkUseTab",         s.EditorUseTabCharacter);
        SetCheck("ChkSmoothScroll",   s.EditorSmoothScrolling);
        SetCheck("ChkCtrlWheel",      s.EditorZoomWithCtrlWheel);

        SetText("TxtRightMargin",     s.EditorRightMarginColumn.ToString());
        SetText("TxtTabSize",         s.EditorTabSize.ToString());
        SetText("TxtIndentSize",      s.EditorIndentSize.ToString());
        SetText("TxtAutoSaveInterval",s.EditorAutoSaveIntervalSeconds.ToString());
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        s.EditorShowLineNumbers       = GetCheck("ChkLineNumbers");
        s.EditorShowIndentGuides      = GetCheck("ChkIndentGuides");
        s.EditorHighlightCurrentLine  = GetCheck("ChkHighlightLine");
        s.EditorShowMatchingBraces    = GetCheck("ChkMatchBraces");
        s.EditorShowWhitespace        = GetCheck("ChkWhitespace");
        s.EditorAutoSave              = GetCheck("ChkAutoSave");
        s.EditorUseTabCharacter       = GetCheck("ChkUseTab");
        s.EditorSmoothScrolling       = GetCheck("ChkSmoothScroll");
        s.EditorZoomWithCtrlWheel     = GetCheck("ChkCtrlWheel");

        if (int.TryParse(GetText("TxtRightMargin"),      out var rm))  s.EditorRightMarginColumn          = rm;
        if (int.TryParse(GetText("TxtTabSize"),          out var ts))  s.EditorTabSize                    = ts;
        if (int.TryParse(GetText("TxtIndentSize"),       out var ind)) s.EditorIndentSize                 = ind;
        if (int.TryParse(GetText("TxtAutoSaveInterval"), out var asi)) s.EditorAutoSaveIntervalSeconds    = asi;

        ApplyToAllDocuments();
    }

    private static void ApplyToAllDocuments()
    {
        var s = Scadix.Designer.Settings.Default;
        foreach (var doc in MainWindowViewModel.Instance.Documents)
        {
            // Get the DocumentView for this document
            if (!MainWindowViewModel.Instance.Views.TryGetValue(doc, out var view)) continue;
            if (view is not DocumentView docView) continue;
            
            // Access the XamlEditorView
            var editorView = docView.FindControl<XamlEditorView>("uxXamlEditor");
            var editor = editorView?.Editor;
            if (editor == null) continue;
            
            // Apply settings
            editor.ShowLineNumbers = s.EditorShowLineNumbers;
            editor.Options.ShowTabs = s.EditorShowWhitespace;
            editor.Options.ShowSpaces = s.EditorShowWhitespace;
            editor.Options.HighlightCurrentLine = s.EditorHighlightCurrentLine;
            editor.Options.IndentationSize = s.EditorIndentSize;
            editor.Options.ConvertTabsToSpaces = !s.EditorUseTabCharacter;
        }
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
}
