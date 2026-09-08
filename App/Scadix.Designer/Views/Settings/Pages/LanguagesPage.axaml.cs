using Avalonia.Controls;

namespace Scadix.Designer.Views.Settings;

public partial class LanguagesPage : UserControl
{
    public LanguagesPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        // C#
        CSharpCodeAnalysisCheckBox.IsChecked = s.CSharpEnableCodeAnalysis;
        CSharpInlayHintsCheckBox.IsChecked = s.CSharpShowInlayHints;
        CSharpAutoImportCheckBox.IsChecked = s.CSharpAutoImportNamespaces;
        
        // XAML
        XamlIntellisenseCheckBox.IsChecked = s.XamlEnableIntellisense;
        XamlFormatOnPasteCheckBox.IsChecked = s.XamlFormatOnPaste;
        XamlDesignerPreviewCheckBox.IsChecked = s.XamlShowDesignerPreview;
        
        // JSON
        JsonValidateSchemaCheckBox.IsChecked = s.JsonValidateSchema;
        JsonFormatOnSaveCheckBox.IsChecked = s.JsonFormatOnSave;
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        // C#
        s.CSharpEnableCodeAnalysis = CSharpCodeAnalysisCheckBox.IsChecked ?? true;
        s.CSharpShowInlayHints = CSharpInlayHintsCheckBox.IsChecked ?? true;
        s.CSharpAutoImportNamespaces = CSharpAutoImportCheckBox.IsChecked ?? true;
        
        // XAML
        s.XamlEnableIntellisense = XamlIntellisenseCheckBox.IsChecked ?? true;
        s.XamlFormatOnPaste = XamlFormatOnPasteCheckBox.IsChecked ?? true;
        s.XamlShowDesignerPreview = XamlDesignerPreviewCheckBox.IsChecked ?? true;
        
        // JSON
        s.JsonValidateSchema = JsonValidateSchemaCheckBox.IsChecked ?? true;
        s.JsonFormatOnSave = JsonFormatOnSaveCheckBox.IsChecked ?? false;
    }
}
