using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class GitPage : UserControl
{
    public GitPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        SetText("TxtGitPath", s.GitExecutablePath);
        SetCheck("ChkSignGpg", s.GitSignCommitsWithGpg);
        SetCheck("ChkCommitTemplate", s.GitShowCommitTemplate);
        SetText("TxtDefaultCommit", s.GitDefaultCommitMessage);
        SetCheck("ChkPruneOnFetch", s.GitPruneOnFetch);
        SetCheck("ChkRebaseOnPull", s.GitRebaseOnPull);
        SetText("TxtProtectedBranches", s.GitProtectedBranchPatterns);

        var cmbAutoFetch = this.FindControl<ComboBox>("CmbAutoFetch");
        if (cmbAutoFetch != null) cmbAutoFetch.SelectedIndex = s.GitAutoFetchIntervalIndex;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        s.GitExecutablePath = GetText("TxtGitPath");
        s.GitSignCommitsWithGpg = GetCheck("ChkSignGpg");
        s.GitShowCommitTemplate = GetCheck("ChkCommitTemplate");
        s.GitDefaultCommitMessage = GetText("TxtDefaultCommit");
        s.GitPruneOnFetch = GetCheck("ChkPruneOnFetch");
        s.GitRebaseOnPull = GetCheck("ChkRebaseOnPull");
        s.GitProtectedBranchPatterns = GetText("TxtProtectedBranches");

        var cmbAutoFetch = this.FindControl<ComboBox>("CmbAutoFetch");
        if (cmbAutoFetch != null) s.GitAutoFetchIntervalIndex = cmbAutoFetch.SelectedIndex;
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
