using Avalonia.Controls;
using Avalonia.Interactivity;
using Scadix.Designer.ViewModels;
using Scadix.Designer.Views.Settings;

namespace Scadix.Designer;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    // Page cache — create once, reuse
    private AppearancePage?      _appearancePage;
    private MenusToolbarsPage?   _menusToolbarsPage;
    private SystemSettingsPage?  _systemSettingsPage;
    private NotificationsPage?   _notificationsPage;
    private EditorGeneralPage?   _editorGeneralPage;
    private EditorFontPage?      _editorFontPage;
    private ColorSchemePage?     _colorSchemePage;
    private CodeStylePage?       _codeStylePage;
    private FileEncodingsPage?   _fileEncodingsPage;
    private GitPage?             _gitPage;
    private CommitPage?          _commitPage;
    private GitHubPage?          _githubPage;
    private BuildPage?           _buildPage;
    private CompilerPage?        _compilerPage;
    private DebuggerPage?        _debuggerPage;
    private KeymapPage?          _keymapPage;
    private ParsingEnginePage?   _parsingEnginePage;
    private PluginsPage?         _pluginsPage;
    private LanguagesPage?       _languagesPage;
    private ToolsPage?           _toolsPage;
    private NuGetPage?           _nugetPage;
    private AdvancedPage?        _advancedPage;
    private PageSizeSettings? _pageSizeSettings;

    public SettingsWindow()
    {
        InitializeComponent();
        _vm = new SettingsViewModel();
        DataContext = _vm;

        // Show default page
        ShowPage("appearance");
    }

    // ── Nav tree selection ────────────────────────────────────────────────
    private void NavTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not SettingsPageNode node) return;
        if (node.HasChildren) return; // group node — don't switch

        ShowPage(node.PageId);
    }

    private void ShowPage(string pageId)
    {
        var area = this.FindControl<ContentControl>("ContentArea");
        if (area == null) return;

        area.Content = pageId switch
        {
            "appearance"     => _appearancePage      ??= new AppearancePage(),
            "menus"          => _menusToolbarsPage   ??= new MenusToolbarsPage(),
            "system"         => _systemSettingsPage  ??= new SystemSettingsPage(),
            "notifications"  => _notificationsPage   ??= new NotificationsPage(),
            "editor_general" => _editorGeneralPage   ??= new EditorGeneralPage(),
            "editor_font"    => _editorFontPage      ??= new EditorFontPage(),
            "editor_colors"  => _colorSchemePage     ??= new ColorSchemePage(),
            "editor_codestyle" => _codeStylePage     ??= new CodeStylePage(),
            "editor_encoding" => _fileEncodingsPage  ??= new FileEncodingsPage(),
            "git"            => _gitPage             ??= new GitPage(),
            "commit"         => _commitPage          ??= new CommitPage(),
            "github"         => _githubPage          ??= new GitHubPage(),
            "build"          => _buildPage           ??= new BuildPage(),
            "compiler"       => _compilerPage        ??= new CompilerPage(),
            "debugger"       => _debuggerPage        ??= new DebuggerPage(),
            "keymap"         => _keymapPage          ??= new KeymapPage(),
            "parsing_engine" => _parsingEnginePage   ??= new ParsingEnginePage(),
            "plugins"        => _pluginsPage         ??= new PluginsPage(),
            "csharp"         => _languagesPage       ??= new LanguagesPage(),
            "xaml"           => _languagesPage       ??= new LanguagesPage(),
            "json"           => _languagesPage       ??= new LanguagesPage(),
            "terminal"       => _toolsPage           ??= new ToolsPage(),
            "nuget"          => _nugetPage           ??= new NuGetPage(),
            "database"       => _toolsPage           ??= new ToolsPage(),
            "external_tools" => _toolsPage           ??= new ToolsPage(),
            "advanced"       => _advancedPage        ??= new AdvancedPage(),
            "pageSize_settings" => _pageSizeSettings ??= new PageSizeSettings(),
            _                => BuildPlaceholderPage(pageId)
        };
    }

    private static UserControl BuildPlaceholderPage(string pageId)
    {
        var label = System.Globalization.CultureInfo.CurrentCulture
                         .TextInfo.ToTitleCase(pageId.Replace('_', ' '));
        return new UserControl
        {
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        FontSize = 18,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                        Foreground = Avalonia.Media.Brushes.White,
                        Margin = new Avalonia.Thickness(0, 0, 0, 16)
                    },
                    new TextBlock
                    {
                        Text = "This settings page is coming soon.",
                        FontSize = 13,
                        Foreground = new Avalonia.Media.SolidColorBrush(
                            Avalonia.Media.Color.Parse("#888888"))
                    }
                }
            }
        };
    }

    // ── Footer buttons ────────────────────────────────────────────────────
    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        SaveAllSettings();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();

    private void OK_Click(object? sender, RoutedEventArgs e)
    {
        SaveAllSettings();
        Close();
    }

    private void SaveAllSettings()
    {
        // Save all pages
        _appearancePage?.SaveSettings();
        _menusToolbarsPage?.SaveSettings();
        _systemSettingsPage?.SaveSettings();
        _notificationsPage?.SaveSettings();
        _editorGeneralPage?.SaveSettings();
        _editorFontPage?.SaveSettings();
        _colorSchemePage?.SaveSettings();
        _codeStylePage?.SaveSettings();
        _fileEncodingsPage?.SaveSettings();
        _gitPage?.SaveSettings();
        _commitPage?.SaveSettings();
        _githubPage?.SaveSettings();
        _buildPage?.SaveSettings();
        _compilerPage?.SaveSettings();
        _debuggerPage?.SaveSettings();
        _parsingEnginePage?.SaveSettings();
        _keymapPage?.SaveSettings();
        _pluginsPage?.SaveSettings();
        _languagesPage?.SaveSettings();
        _toolsPage?.SaveSettings();
        _nugetPage?.SaveSettings();
        _advancedPage?.SaveSettings();
        _pageSizeSettings?.SaveSettings();
        // Persist to disk
        Scadix.Designer.Settings.Default.Save();
    }
}
