using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class CommitPage : UserControl
{
    public CommitPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        SetCheck("ChkUseNonModalCommit", s.CommitUseNonModalInterface);
        SetCheck("ChkShowUnversionedFiles", s.CommitShowUnversionedFiles);
        SetCheck("ChkHighlightSpellingErrors", s.CommitHighlightSpellingErrors);
        SetCheck("ChkAnalyzeCodeBeforeCommit", s.CommitAnalyzeCode);
        SetCheck("ChkCheckTodoBeforeCommit", s.CommitCheckTodo);
        SetCheck("ChkOptimizeImportsBeforeCommit", s.CommitOptimizeImports);
        SetCheck("ChkRightMarginInCommitMessage", s.CommitShowRightMargin);
        SetText("TxtCommitMessageRightMargin", s.CommitMessageRightMarginColumn.ToString());
        SetCheck("ChkWrapCommitMessage", s.CommitWrapOnTyping);
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        s.CommitUseNonModalInterface = GetCheck("ChkUseNonModalCommit");
        s.CommitShowUnversionedFiles = GetCheck("ChkShowUnversionedFiles");
        s.CommitHighlightSpellingErrors = GetCheck("ChkHighlightSpellingErrors");
        s.CommitAnalyzeCode = GetCheck("ChkAnalyzeCodeBeforeCommit");
        s.CommitCheckTodo = GetCheck("ChkCheckTodoBeforeCommit");
        s.CommitOptimizeImports = GetCheck("ChkOptimizeImportsBeforeCommit");
        s.CommitShowRightMargin = GetCheck("ChkRightMarginInCommitMessage");
        if (int.TryParse(GetText("TxtCommitMessageRightMargin"), out var rm)) s.CommitMessageRightMarginColumn = rm;
        s.CommitWrapOnTyping = GetCheck("ChkWrapCommitMessage");
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
