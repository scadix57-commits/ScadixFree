using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class ToolsPage : UserControl
{
    public ToolsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        // Terminal
        TerminalShellPathTextBox.Text = s.TerminalShellPath;
        TerminalStartupDirTextBox.Text = s.TerminalStartupDirectory;
        TerminalCursorBlinkCheckBox.IsChecked = s.TerminalCursorBlink;
        TerminalScrollbackNumeric.Value = s.TerminalScrollbackLines;
        
        // External Tools
        ExternalDiffToolTextBox.Text = s.ExternalDiffTool;
        ExternalMergeToolTextBox.Text = s.ExternalMergeTool;
        
        // Database
        DatabaseAutoConnectCheckBox.IsChecked = s.DatabaseAutoConnectOnStartup;
        DatabaseTimeoutNumeric.Value = s.DatabaseQueryTimeoutSeconds;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        // Terminal
        s.TerminalShellPath = TerminalShellPathTextBox.Text ?? string.Empty;
        s.TerminalStartupDirectory = TerminalStartupDirTextBox.Text ?? string.Empty;
        s.TerminalCursorBlink = TerminalCursorBlinkCheckBox.IsChecked ?? true;
        s.TerminalScrollbackLines = (int)(TerminalScrollbackNumeric.Value ?? 1000);
        
        // External Tools
        s.ExternalDiffTool = ExternalDiffToolTextBox.Text ?? string.Empty;
        s.ExternalMergeTool = ExternalMergeToolTextBox.Text ?? string.Empty;
        
        // Database
        s.DatabaseAutoConnectOnStartup = DatabaseAutoConnectCheckBox.IsChecked ?? false;
        s.DatabaseQueryTimeoutSeconds = (int)(DatabaseTimeoutNumeric.Value ?? 30);
    }
}
