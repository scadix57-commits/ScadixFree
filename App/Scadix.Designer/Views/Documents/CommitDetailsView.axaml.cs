using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using Scadix.Designer.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Scadix.Designer.Services;
using Scadix.Designer.Views.Tools;
using Scadix.Designer.Models;

namespace Scadix.Designer.Views.Documents;

public partial class CommitDetailsView : UserControl
{

    public CommitDetailsView()
    {
        InitializeComponent();
    }

    // ── File selected in tree → load diff ────────────────────────────────

    private void OnFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TreeView tree) return;
        if (tree.SelectedItem is not GitChangeNode node) return;
        if (node.IsFolder) return;

        var vm = DataContext as GitRepositoriesViewModel;
        if (vm?.SelectedCommit == null) return;

        _ = LoadFileDiffAsync(vm, node);
    }

    private void OnFileDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is not TreeView tree) return;
        if (tree.SelectedItem is not GitChangeNode node) return;
        if (node.IsFolder) return;

        // Open diff in a new tab
        MainWindowViewModel.Instance.Factory?.OpenDiff(node);
        e.Handled = true;
    }

    private async Task LoadFileDiffAsync(GitRepositoriesViewModel vm, GitChangeNode node)

    {

        var leftLabel = this.FindControl<TextBlock>("LeftFileLabel");

        var rightLabel = this.FindControl<TextBlock>("RightFileLabel");

        var leftEditor = this.FindControl<XamlEditorView>("LeftEditor");

        var rightEditor = this.FindControl<XamlEditorView>("RightEditor");

        if (leftEditor == null || rightEditor == null) return;

        var fileName = node.Name;
        var relPath = node.FilePath ?? node.Change?.FilePath ?? node.Name;

        var hash = vm.SelectedCommit!.Hash;

        var parentHash = vm.SelectedCommit.Parents.Count > 0

            ? vm.SelectedCommit.Parents[0]

            : null;

        // Update labels

        if (leftLabel != null) leftLabel.Text = $"{fileName} ({(parentHash != null ? parentHash[..7] : "empty")})";

        if (rightLabel != null) rightLabel.Text = $"{fileName} ({hash[..7]})";

        // Get repo path

        var repoPath = GetRepoPath(vm);

        if (string.IsNullOrEmpty(repoPath)) return;

        // Fetch both versions

        var leftText = parentHash != null

            ? await RunGitAsync(repoPath, $"show {parentHash}:{relPath}")

            : "";

        var rightText = await RunGitAsync(repoPath, $"show {hash}:{relPath}");

        // Set syntax highlighting

        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        var highlighting = GetHighlighting(ext);

        // Load into editors via SetText (read-only mode)
        Dispatcher.UIThread.Post(() =>
        {
            var diff = DiffService.ComputeSideBySide(leftText, rightText);
            SetEditorText(leftEditor, leftText, highlighting, diff.Left);
            SetEditorText(rightEditor, rightText, highlighting, diff.Right);

            SetupSyncScrolling(leftEditor, rightEditor);
        });
    }

    private bool _isSyncScrollingSetup = false;
    private void SetupSyncScrolling(XamlEditorView leftView, XamlEditorView rightView)
    {
        if (_isSyncScrollingSetup) return;
        var left = leftView.Editor;
        var right = rightView.Editor;
        if (left == null || right == null) return;

        var leftScroll = left.FindDescendantOfType<ScrollViewer>();
        var rightScroll = right.FindDescendantOfType<ScrollViewer>();

        if (leftScroll == null || rightScroll == null) return;
        _isSyncScrollingSetup = true;

        bool isSyncing = false;

        leftScroll.PropertyChanged += (s, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty)
            {
                if (isSyncing) return;
                isSyncing = true;
                rightScroll.Offset = (Vector)e.NewValue!;
                isSyncing = false;
            }
        };

        rightScroll.PropertyChanged += (s, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty)
            {
                if (isSyncing) return;
                isSyncing = true;
                leftScroll.Offset = (Vector)e.NewValue!;
                isSyncing = false;
            }
        };
    }

    private static void SetEditorText(XamlEditorView editorView, string text,
        IHighlightingDefinition? highlighting, System.Collections.ObjectModel.ObservableCollection<DiffLine> diffLines)
    {
        var editor = editorView.Editor;
        if (editor == null) return;
        editor.IsReadOnly = true;
        editor.SyntaxHighlighting = highlighting;
        editor.Text = text;

        // Apply diff highlighting
        editor.TextArea.TextView.BackgroundRenderers.Clear();
        editor.TextArea.TextView.BackgroundRenderers.Add(new DiffBackgroundRenderer(editor, diffLines));
        editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
    }

    private static string GetRepoPath(GitRepositoriesViewModel vm)

    {

        if (MainWindowViewModel.Instance?.SolutionTree == null) return "";

        foreach (var node in MainWindowViewModel.Instance.SolutionTree)

        {

            var r = FindRepo(node);

            if (!string.IsNullOrEmpty(r)) return r;

        }

        return "";

    }

    private static string FindRepo(SolutionNode node)

    {

        var current = File.Exists(node.FilePath)

            ? Path.GetDirectoryName(node.FilePath)

            : node.FilePath;

        while (!string.IsNullOrEmpty(current))

        {

            if (Directory.Exists(Path.Combine(current, ".git"))) return current;

            var parent = Path.GetDirectoryName(current);

            if (parent == current) break;

            current = parent;

        }

        if (node.Children != null)

            foreach (var child in node.Children)

            {

                var r = FindRepo(child);

                if (!string.IsNullOrEmpty(r)) return r;

            }

        return "";

    }

    private static async Task<string> RunGitAsync(string workingDir, string arguments)

    {

        try

        {

            var si = new ProcessStartInfo("git", arguments)

            {

                WorkingDirectory = workingDir,

                RedirectStandardOutput = true,

                RedirectStandardError = true,

                UseShellExecute = false,

                CreateNoWindow = true

            };

            using var p = Process.Start(si);

            if (p == null) return "";

            var output = await p.StandardOutput.ReadToEndAsync();

            await p.WaitForExitAsync();

            return output;

        }

        catch { return ""; }

    }

    private static IHighlightingDefinition? GetHighlighting(string ext) => ext switch

    {

        ".cs" => HighlightingManager.Instance.GetDefinitionByExtension(".cs"),

        ".xaml" or ".axaml" => HighlightingManager.Instance.GetDefinitionByExtension(".xml"),

        ".xml" => HighlightingManager.Instance.GetDefinitionByExtension(".xml"),

        ".json" => HighlightingManager.Instance.GetDefinitionByExtension(".json"),

        ".js" or ".ts" => HighlightingManager.Instance.GetDefinitionByExtension(".js"),

        _ => null

    };

}