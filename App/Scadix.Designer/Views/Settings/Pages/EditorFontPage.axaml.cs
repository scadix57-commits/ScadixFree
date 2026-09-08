using Avalonia.Controls;
using Avalonia.Media;

namespace Scadix.Designer.Views.Settings;

public partial class EditorFontPage : UserControl
{
    public EditorFontPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        var cmbFamily = this.FindControl<ComboBox>("CmbFontFamily");
        var cmbSize   = this.FindControl<ComboBox>("CmbFontSize");
        var cmbHeight = this.FindControl<ComboBox>("CmbLineHeight");
        var chkLig    = this.FindControl<CheckBox>("ChkLigatures");
        var chkAA     = this.FindControl<CheckBox>("ChkAntiAlias");

        if (cmbFamily != null)
        {
            cmbFamily.SelectedIndex = s.EditorFontFamily switch
            {
                "JetBrains Mono" => 0,
                "Cascadia Code" => 1,
                "Consolas" => 2,
                "Fira Code" => 3,
                "Source Code Pro" => 4,
                _ => 0
            };
        }
        if (cmbSize   != null) cmbSize.SelectedIndex   = s.EditorFontSizeIndex;
        if (cmbHeight != null) cmbHeight.SelectedIndex = s.EditorLineHeightIndex;
        if (chkLig    != null) chkLig.IsChecked        = s.EditorFontLigatures;
        if (chkAA     != null) chkAA.IsChecked         = s.EditorAntiAliasing;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        var cmbFamily = this.FindControl<ComboBox>("CmbFontFamily");
        var cmbSize   = this.FindControl<ComboBox>("CmbFontSize");
        var cmbHeight = this.FindControl<ComboBox>("CmbLineHeight");
        var chkLig    = this.FindControl<CheckBox>("ChkLigatures");
        var chkAA     = this.FindControl<CheckBox>("ChkAntiAlias");

        if (cmbFamily != null)
        {
            s.EditorFontFamily = cmbFamily.SelectedIndex switch
            {
                0 => "JetBrains Mono",
                1 => "Cascadia Code",
                2 => "Consolas",
                3 => "Fira Code",
                4 => "Source Code Pro",
                _ => "Consolas"
            };
        }
        if (cmbSize   != null) s.EditorFontSizeIndex   = cmbSize.SelectedIndex;
        if (cmbHeight != null) s.EditorLineHeightIndex  = cmbHeight.SelectedIndex;
        if (chkLig    != null) s.EditorFontLigatures    = chkLig.IsChecked == true;
        if (chkAA     != null) s.EditorAntiAliasing     = chkAA.IsChecked  == true;

        ApplyToAllDocuments();
    }

    private static void ApplyToAllDocuments()
    {
        var s = Scadix.Designer.Settings.Default;
        double fontSize = s.EditorFontSizeIndex switch
        {
            0 => 10, 1 => 11, 2 => 12, 3 => 13, 4 => 14, 5 => 16, _ => 12
        };
        var fontFamily = new FontFamily(s.EditorFontFamily);

        foreach (var doc in MainWindowViewModel.Instance.Documents)
        {
            // Get the DocumentView for this document
            if (!MainWindowViewModel.Instance.Views.TryGetValue(doc, out var view)) continue;
            if (view is not DocumentView docView) continue;
            
            // Access the XamlEditorView
            var editorView = docView.FindControl<XamlEditorView>("uxXamlEditor");
            var editor = editorView?.Editor;
            if (editor == null) continue;
            
            // Apply font settings
            editor.FontFamily = fontFamily;
            editor.FontSize = fontSize;
        }
    }
}
