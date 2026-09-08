using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Scadix.Designer.Views.Settings;

public partial class DebuggerPage : UserControl
{
    public DebuggerPage()
    {
        InitializeComponent();
        LoadSettings();
        AttachEventHandlers();
    }

    private void AttachEventHandlers()
    {
        var btnBrowseSymbolCache = this.FindControl<Button>("BtnBrowseSymbolCache");
        if (btnBrowseSymbolCache != null)
            btnBrowseSymbolCache.Click += BtnBrowseSymbolCache_Click;

        var btnBrowseNetCoreDbg = this.FindControl<Button>("BtnBrowseNetCoreDbg");
        if (btnBrowseNetCoreDbg != null)
            btnBrowseNetCoreDbg.Click += BtnBrowseNetCoreDbg_Click;

        var btnBrowseLogPath = this.FindControl<Button>("BtnBrowseLogPath");
        if (btnBrowseLogPath != null)
            btnBrowseLogPath.Click += BtnBrowseLogPath_Click;
    }

    private void LoadSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        // General
        SetCheck("ChkShowDebugWindow", s.DebuggerShowDebugWindow);
        SetCheck("ChkFocusApplicationOnBreakpoint", s.DebuggerFocusApplicationOnBreakpoint);
        
        // Evaluation
        SetCheck("ChkEnablePropertyEvaluation", s.DebuggerEnablePropertyEvaluation);
        SetCheck("ChkEnableToStringCalls", s.DebuggerEnableToStringCalls);
        SetCheck("ChkEnableImplicitFunctionEvaluation", s.DebuggerEnableImplicitFunctionEvaluation);
        
        // Stepping
        SetCheck("ChkStepOverPropertiesAndOperators", s.DebuggerStepOverPropertiesAndOperators);
        SetCheck("ChkStepOverSystemCode", s.DebuggerStepOverSystemCode);
        
        // Symbols
        SetCheck("ChkLoadSymbolsAutomatically", s.DebuggerLoadSymbolsAutomatically);
        SetText("TxtSymbolCache", s.DebuggerSymbolCacheDirectory);
        
        // NetCoreDbg
        SetCheck("ChkUseNetCoreDbg", s.DebuggerUseNetCoreDbg);
        SetText("TxtNetCoreDbgPath", s.DebuggerNetCoreDbgPath);
        SetNumeric("NumNetCoreDbgPort", s.DebuggerNetCoreDbgPort);
        SetCheck("ChkNetCoreDbgAutoStart", s.DebuggerNetCoreDbgAutoStart);
        SetCheck("ChkNetCoreDbgEnableLogging", s.DebuggerNetCoreDbgEnableLogging);
        SetText("TxtNetCoreDbgLogPath", s.DebuggerNetCoreDbgLogPath);
        SetText("TxtNetCoreDbgArgs", s.DebuggerNetCoreDbgArgs);
        
        // XAML Debugging
        SetCheck("ChkEnableXamlDebugging", s.DebuggerEnableXamlDebugging);
        SetCheck("ChkXamlBreakOnErrors", s.DebuggerXamlBreakOnErrors);
        SetCheck("ChkXamlShowBindingErrors", s.DebuggerXamlShowBindingErrors);
    }

    public void SaveSettings()
    {
        var s = Scadix.Designer.Settings.Default;
        
        // General
        s.DebuggerShowDebugWindow = GetCheck("ChkShowDebugWindow");
        s.DebuggerFocusApplicationOnBreakpoint = GetCheck("ChkFocusApplicationOnBreakpoint");
        
        // Evaluation
        s.DebuggerEnablePropertyEvaluation = GetCheck("ChkEnablePropertyEvaluation");
        s.DebuggerEnableToStringCalls = GetCheck("ChkEnableToStringCalls");
        s.DebuggerEnableImplicitFunctionEvaluation = GetCheck("ChkEnableImplicitFunctionEvaluation");
        
        // Stepping
        s.DebuggerStepOverPropertiesAndOperators = GetCheck("ChkStepOverPropertiesAndOperators");
        s.DebuggerStepOverSystemCode = GetCheck("ChkStepOverSystemCode");
        
        // Symbols
        s.DebuggerLoadSymbolsAutomatically = GetCheck("ChkLoadSymbolsAutomatically");
        s.DebuggerSymbolCacheDirectory = GetText("TxtSymbolCache");
        
        // NetCoreDbg
        s.DebuggerUseNetCoreDbg = GetCheck("ChkUseNetCoreDbg");
        s.DebuggerNetCoreDbgPath = GetText("TxtNetCoreDbgPath");
        s.DebuggerNetCoreDbgPort = GetNumeric("NumNetCoreDbgPort");
        s.DebuggerNetCoreDbgAutoStart = GetCheck("ChkNetCoreDbgAutoStart");
        s.DebuggerNetCoreDbgEnableLogging = GetCheck("ChkNetCoreDbgEnableLogging");
        s.DebuggerNetCoreDbgLogPath = GetText("TxtNetCoreDbgLogPath");
        s.DebuggerNetCoreDbgArgs = GetText("TxtNetCoreDbgArgs");
        
        // XAML Debugging
        s.DebuggerEnableXamlDebugging = GetCheck("ChkEnableXamlDebugging");
        s.DebuggerXamlBreakOnErrors = GetCheck("ChkXamlBreakOnErrors");
        s.DebuggerXamlShowBindingErrors = GetCheck("ChkXamlShowBindingErrors");
    }

    // ── Event Handlers ───────────────────────────────────────────────────

    private async void BtnBrowseSymbolCache_Click(object? sender, RoutedEventArgs e)
    {
        var folder = await BrowseForFolderAsync("Select Symbol Cache Directory");
        if (!string.IsNullOrEmpty(folder))
            SetText("TxtSymbolCache", folder);
    }

    private async void BtnBrowseNetCoreDbg_Click(object? sender, RoutedEventArgs e)
    {
        var file = await BrowseForFileAsync("Select netcoredbg Executable", 
            new[] { "netcoredbg", "netcoredbg.exe" });
        if (!string.IsNullOrEmpty(file))
            SetText("TxtNetCoreDbgPath", file);
    }

    private async void BtnBrowseLogPath_Click(object? sender, RoutedEventArgs e)
    {
        var folder = await BrowseForFolderAsync("Select Log Output Directory");
        if (!string.IsNullOrEmpty(folder))
            SetText("TxtNetCoreDbgLogPath", folder);
    }

    // ── File/Folder Dialogs ──────────────────────────────────────────────

    private async Task<string?> BrowseForFolderAsync(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    private async Task<string?> BrowseForFileAsync(string title, string[] fileNames)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executable Files")
                {
                    Patterns = fileNames
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*" }
                }
            }
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
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

    private void SetNumeric(string name, int value)
    {
        var n = this.FindControl<NumericUpDown>(name);
        if (n != null) n.Value = value;
    }

    private int GetNumeric(string name)
    {
        var n = this.FindControl<NumericUpDown>(name);
        return (int)(n?.Value ?? 0);
    }
}
