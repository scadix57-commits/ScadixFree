using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class ColorSchemePage : UserControl
{
    public ColorSchemePage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        SchemeComboBox.SelectedIndex = s.ColorSchemeIndex;
        SemanticHighlightingCheckBox.IsChecked = s.ColorSchemeSemanticHighlighting;
        UseColorSchemeFontCheckBox.IsChecked = s.ColorSchemeUseCustomFont;
        FontComboBox.SelectedIndex = s.ColorSchemeFontIndex;
        FontSizeNumeric.Value = s.ColorSchemeFontSize;
        LineHeightNumeric.Value = (decimal)s.ColorSchemeLineHeight;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        s.ColorSchemeIndex = SchemeComboBox.SelectedIndex;
        s.ColorSchemeSemanticHighlighting = SemanticHighlightingCheckBox.IsChecked ?? false;
        s.ColorSchemeUseCustomFont = UseColorSchemeFontCheckBox.IsChecked ?? false;
        s.ColorSchemeFontIndex = FontComboBox.SelectedIndex;
        s.ColorSchemeFontSize = (int)(FontSizeNumeric.Value ?? 13);
        s.ColorSchemeLineHeight = (double)(LineHeightNumeric.Value ?? 1.2m);
    }
}
