using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class GitHubPage : UserControl
{
    public GitHubPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        SetText("TxtGitHubAccount", s.GitHubAccount);
        SetText("TxtGitHubToken", s.GitHubToken);
        SetCheck("ChkCloneUsingSSH", s.GitHubCloneUsingSSH);
        SetText("TxtCloneDirectory", s.GitHubCloneDirectory);
        SetText("TxtTimeout", s.GitHubTimeoutSeconds.ToString());
        SetCheck("ChkShowPullRequestsInToolWindow", s.GitHubShowPullRequestsInToolWindow);
        SetCheck("ChkAutoFetchPullRequests", s.GitHubAutoFetchPullRequests);
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        s.GitHubAccount = GetText("TxtGitHubAccount");
        s.GitHubToken = GetText("TxtGitHubToken");
        s.GitHubCloneUsingSSH = GetCheck("ChkCloneUsingSSH");
        s.GitHubCloneDirectory = GetText("TxtCloneDirectory");
        if (int.TryParse(GetText("TxtTimeout"), out var timeout)) s.GitHubTimeoutSeconds = timeout;
        s.GitHubShowPullRequestsInToolWindow = GetCheck("ChkShowPullRequestsInToolWindow");
        s.GitHubAutoFetchPullRequests = GetCheck("ChkAutoFetchPullRequests");
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
