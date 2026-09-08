using System;
using System.Collections.Specialized;
using System.IO;
using Newtonsoft.Json;
using Scadix.AxamlDom;

namespace Scadix.Designer
{
    public class Settings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scadix.Designer", "settings.json");

        public static Settings Default { get; set; } = Load();

        // ── Recent files ─────────────────────────────────────────────────
        public StringCollection RecentFiles { get; set; } = new();
        public string ProjectName { get; set; }
        public string ProjectPath { get; set; }
        public double LastPageWidth { get; internal set; }
        public double LastPageHeight { get; internal set; }
        public string LastPlatform { get; internal set; }
        public string? DefaultPlatform { get; internal set; }

        // ── Appearance ───────────────────────────────────────────────────
        /// <summary>0=Dark, 1=Light, 2=HighContrast</summary>
        public int ThemeIndex { get; set; } = 1;
        public bool SyncWithOsTheme { get; set; } = false;
        public string UiFont { get; set; } = "Segoe UI";
        public int UiFontSizeIndex { get; set; } = 1;
        public bool ShowStatusBar { get; set; } = true;
        public bool ShowMainToolbar { get; set; } = true;
        public bool ShowToolWindowBars { get; set; } = true;
        /// <summary>0=Top, 1=Bottom, 2=Left, 3=Right</summary>
        public int TabPlacementIndex { get; set; } = 0;

        // ── System Settings ──────────────────────────────────────────────
        public bool ConfirmBeforeExiting { get; set; } = true;
        public bool ReopenProjectsOnStartup { get; set; } = true;
        /// <summary>0=New Window, 1=Current Window, 2=Ask</summary>
        public int OpenProjectInIndex { get; set; } = 2;
        public string DefaultProjectDirectory { get; set; } = string.Empty;
        public bool MoveFilesToBin { get; set; } = true;
        public bool SaveFilesWhenSwitching { get; set; } = true;
        public int AutoSaveIdleSeconds { get; set; } = 15;
        public bool BackupFilesBeforeSaving { get; set; } = true;

        // ── Notifications ────────────────────────────────────────────────
        public bool DisplayBalloonNotifications { get; set; } = true;
        public bool PlaySoundNotifications { get; set; } = false;
        public bool ShowNotificationsInToolWindow { get; set; } = true;

        // ── Editor General ───────────────────────────────────────────────
        public bool EditorZoomWithCtrlWheel { get; set; } = true;
        public bool EditorSmoothScrolling { get; set; } = false;
        public bool EditorShowLineNumbers { get; set; } = true;
        public bool EditorShowWhitespace { get; set; } = false;
        public bool EditorShowIndentGuides { get; set; } = true;
        public bool EditorHighlightCurrentLine { get; set; } = true;
        public bool EditorShowMatchingBraces { get; set; } = true;
        public int EditorRightMarginColumn { get; set; } = 120;
        public bool EditorUseTabCharacter { get; set; } = false;
        public int EditorTabSize { get; set; } = 4;
        public int EditorIndentSize { get; set; } = 4;
        public bool EditorAutoSave { get; set; } = true;
        public int EditorAutoSaveIntervalSeconds { get; set; } = 30;

        // ── Editor Font ──────────────────────────────────────────────────
        public string EditorFontFamily { get; set; } = "JetBrains Mono";
        public int EditorFontSizeIndex { get; set; } = 2;
        public int EditorLineHeightIndex { get; set; } = 1;
        public bool EditorFontLigatures { get; set; } = true;
        public bool EditorAntiAliasing { get; set; } = true;

        // ── Color Scheme ─────────────────────────────────────────────────
        /// <summary>0=Darcula, 1=IntelliJ Light, 2=High Contrast, 3=VS Dark, 4=VS Light, 5=Rider Dark, 6=Rider Light</summary>
        public int ColorSchemeIndex { get; set; } = 0;
        public bool ColorSchemeSemanticHighlighting { get; set; } = false;
        public bool ColorSchemeUseCustomFont { get; set; } = false;
        public int ColorSchemeFontIndex { get; set; } = 0;
        public int ColorSchemeFontSize { get; set; } = 13;
        public double ColorSchemeLineHeight { get; set; } = 1.2;

        // ── Build ────────────────────────────────────────────────────────
        public bool BuildBeforeRun { get; set; } = true;
        public bool BuildShowOutputAutomatically { get; set; } = true;
        public bool BuildClearOutputBeforeBuild { get; set; } = true;
        /// <summary>0=Debug, 1=Release</summary>
        public int BuildConfigurationIndex { get; set; } = 0;
        /// <summary>0=Any CPU, 1=x64, 2=x86, 3=ARM64</summary>
        public int BuildPlatformIndex { get; set; } = 0;
        public bool BuildEnableNullable { get; set; } = true;
        public bool BuildEnableImplicitUsings { get; set; } = true;
        public string DotNetSdkPath { get; set; } = string.Empty;

        // ── Git ──────────────────────────────────────────────────────────
        public string GitExecutablePath { get; set; } = "git";
        public bool GitSignCommitsWithGpg { get; set; } = false;
        public bool GitShowCommitTemplate { get; set; } = true;
        public string GitDefaultCommitMessage { get; set; } = string.Empty;
        /// <summary>0=Off, 1=5min, 2=10min, 3=30min</summary>
        public int GitAutoFetchIntervalIndex { get; set; } = 1;
        public bool GitPruneOnFetch { get; set; } = true;
        public bool GitRebaseOnPull { get; set; } = false;
        public string GitProtectedBranchPatterns { get; set; } = "main;master;release/*";

        // ── Parsing Engine ───────────────────────────────────────────────
        public XamlParsingEngine SelectedParsingEngine { get; set; } = XamlParsingEngine.Automatic;
        public bool DeepResourceResolution { get; set; } = true;
        public bool BackgroundAssemblyPreload { get; set; } = true;
        public bool ReloadDesignerAfterEngineChange { get; set; } = true;

        // ── Keymap ───────────────────────────────────────────────────────
        /// <summary>0=Scadix Default, 1=Visual Studio, 2=JetBrains Rider, 3=VS Code</summary>
        public int KeymapSchemeIndex { get; set; } = 0;

        // ── Plugins ──────────────────────────────────────────────────────
        public bool PluginsAutoUpdate { get; set; } = true;
        public bool PluginsCheckForUpdatesOnStartup { get; set; } = true;
        public string PluginsCustomRepositoryUrl { get; set; } = string.Empty;

        // ── Languages & Frameworks ───────────────────────────────────────
        // C#
        public bool CSharpEnableCodeAnalysis { get; set; } = true;
        public bool CSharpShowInlayHints { get; set; } = true;
        public bool CSharpAutoImportNamespaces { get; set; } = true;
        // XAML
        public bool XamlEnableIntellisense { get; set; } = true;
        public bool XamlFormatOnPaste { get; set; } = true;
        public bool XamlShowDesignerPreview { get; set; } = true;
        // JSON
        public bool JsonValidateSchema { get; set; } = true;
        public bool JsonFormatOnSave { get; set; } = false;

        // ── Tools ────────────────────────────────────────────────────────
        // Terminal
        public string TerminalShellPath { get; set; } = string.Empty;
        public string TerminalStartupDirectory { get; set; } = string.Empty;
        public bool TerminalCursorBlink { get; set; } = true;
        public int TerminalScrollbackLines { get; set; } = 1000;
        // External Tools
        public string ExternalDiffTool { get; set; } = string.Empty;
        public string ExternalMergeTool { get; set; } = string.Empty;
        // NuGet
        public bool NuGetIncludePrerelease { get; set; } = false;
        public bool NuGetIncludeUnlisted { get; set; } = false;
        public bool NuGetAutoRestoreMissingPackages { get; set; } = true;
        public bool NuGetAutoRestoreBeforeBuild { get; set; } = true;
        /// <summary>0=Ignore, 1=Lowest, 2=HighestPatch, 3=HighestMinor, 4=Highest</summary>
        public int NuGetDependencyBehaviorIndex { get; set; } = 1;
        /// <summary>0=packages.config, 1=PackageReference, 2=Auto</summary>
        public int NuGetPackageFormatIndex { get; set; } = 2;
        // Database
        public bool DatabaseAutoConnectOnStartup { get; set; } = false;
        public int DatabaseQueryTimeoutSeconds { get; set; } = 30;

        // ── Advanced ─────────────────────────────────────────────────────
        public bool AdvancedShowMemoryIndicator { get; set; } = false;
        public bool AdvancedEnableExperimentalFeatures { get; set; } = false;
        public int AdvancedMaxMemoryMB { get; set; } = 2048;
        public bool AdvancedEnableLogging { get; set; } = false;
        public string AdvancedLogLevel { get; set; } = "Info";

        // ── Code Style ───────────────────────────────────────────────────
        /// <summary>0=Project, 1=Default</summary>
        public int CodeStyleSchemeIndex { get; set; } = 0;
        public int CodeStyleTabSize { get; set; } = 4;
        public int CodeStyleIndentSize { get; set; } = 4;
        public int CodeStyleContinuationIndent { get; set; } = 8;
        public bool CodeStyleKeepIndentsOnEmptyLines { get; set; } = false;
        public bool CodeStyleSpaceBeforeMethodParentheses { get; set; } = false;
        public bool CodeStyleSpaceBeforeMethodCallParentheses { get; set; } = false;
        public bool CodeStyleSpaceWithinMethodParentheses { get; set; } = false;
        public bool CodeStyleSpaceWithinBrackets { get; set; } = true;
        public int CodeStyleHardWrapAt { get; set; } = 120;
        public bool CodeStyleWrapOnTyping { get; set; } = true;
        /// <summary>0=End of line, 1=Next line, 2=Next line indented</summary>
        public int CodeStyleBracesPlacementIndex { get; set; } = 0;

        // ── File Encodings ───────────────────────────────────────────────
        /// <summary>0=UTF-8, 1=UTF-16, 2=UTF-32, 3=Windows-1252, 4=ISO-8859-1, 5=ASCII</summary>
        public int FileEncodingGlobalIndex { get; set; } = 0;
        /// <summary>0=UTF-8, 1=UTF-16, 2=UTF-32, 3=Windows-1252, 4=ISO-8859-1, 5=ASCII</summary>
        public int FileEncodingProjectIndex { get; set; } = 0;
        /// <summary>0=with BOM, 1=without BOM, 2=with BOM on Windows</summary>
        public int FileEncodingUtf8BomModeIndex { get; set; } = 1;
        public bool FileEncodingTransparentNativeToAscii { get; set; } = false;
        public bool FileEncodingAutoDetect { get; set; } = true;

        // ── Compiler ─────────────────────────────────────────────────────
        public int CompilerHeapSizeMB { get; set; } = 1024;
        public bool CompilerParallelCompilation { get; set; } = true;
        public string CompilerOutputPath { get; set; } = "bin/Debug";
        public string CompilerAdditionalArgs { get; set; } = string.Empty;
        public bool CompilerTreatWarningsAsErrors { get; set; } = false;
        /// <summary>0-4 warning level</summary>
        public int CompilerWarningLevelIndex { get; set; } = 4;

        // ── Debugger ─────────────────────────────────────────────────────
        public bool DebuggerShowDebugWindow { get; set; } = true;
        public bool DebuggerFocusApplicationOnBreakpoint { get; set; } = false;
        public bool DebuggerEnablePropertyEvaluation { get; set; } = true;
        public bool DebuggerEnableToStringCalls { get; set; } = true;
        public bool DebuggerEnableImplicitFunctionEvaluation { get; set; } = false;
        public bool DebuggerStepOverPropertiesAndOperators { get; set; } = true;
        public bool DebuggerStepOverSystemCode { get; set; } = true;
        public bool DebuggerLoadSymbolsAutomatically { get; set; } = true;
        public string DebuggerSymbolCacheDirectory { get; set; } = string.Empty;
        
        // NetCoreDbg Settings
        public bool DebuggerUseNetCoreDbg { get; set; } = false;
        public string DebuggerNetCoreDbgPath { get; set; } = "netcoredbg";
        public int DebuggerNetCoreDbgPort { get; set; } = 4711;
        public bool DebuggerNetCoreDbgAutoStart { get; set; } = true;
        public bool DebuggerNetCoreDbgEnableLogging { get; set; } = false;
        public string DebuggerNetCoreDbgLogPath { get; set; } = string.Empty;
        public string DebuggerNetCoreDbgArgs { get; set; } = string.Empty;
        
        // XAML Debugging
        public bool DebuggerEnableXamlDebugging { get; set; } = false;
        public bool DebuggerXamlBreakOnErrors { get; set; } = false;
        public bool DebuggerXamlShowBindingErrors { get; set; } = true;

        // ── Commit ───────────────────────────────────────────────────────
        public bool CommitUseNonModalInterface { get; set; } = true;
        public bool CommitShowUnversionedFiles { get; set; } = true;
        public bool CommitHighlightSpellingErrors { get; set; } = true;
        public bool CommitAnalyzeCode { get; set; } = false;
        public bool CommitCheckTodo { get; set; } = false;
        public bool CommitOptimizeImports { get; set; } = false;
        public bool CommitShowRightMargin { get; set; } = true;
        public int CommitMessageRightMarginColumn { get; set; } = 72;
        public bool CommitWrapOnTyping { get; set; } = false;

        // ── GitHub ───────────────────────────────────────────────────────
        public string GitHubAccount { get; set; } = string.Empty;
        public string GitHubToken { get; set; } = string.Empty;
        public bool GitHubCloneUsingSSH { get; set; } = true;
        public string GitHubCloneDirectory { get; set; } = string.Empty;
        public int GitHubTimeoutSeconds { get; set; } = 30;
        public bool GitHubShowPullRequestsInToolWindow { get; set; } = true;
        public bool GitHubAutoFetchPullRequests { get; set; } = false;

        // ── Persistence ──────────────────────────────────────────────────
        public static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
                }
            }
            catch { }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { }
        }
    }

    
}
