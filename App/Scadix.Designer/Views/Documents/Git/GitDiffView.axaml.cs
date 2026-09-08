using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;
using Scadix.Designer.ViewModels;
using Scadix.Designer.Models;
using Scadix.Designer.Views.Tools;

namespace Scadix.Designer.Views.Documents;

public partial class GitDiffView : UserControl
{
    private XamlEditorView? _leftEditor;
    private XamlEditorView? _rightEditor;

    public GitDiffView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not GitDiffDocumentViewModel vm) return;

        _leftEditor  = this.FindControl<XamlEditorView>("LeftEditor");
        _rightEditor = this.FindControl<XamlEditorView>("RightEditor");

        // Subscribe to loading completion
        vm.PropertyChanged += (_, pe) =>
        {
            if (pe.PropertyName == nameof(GitDiffDocumentViewModel.IsLoading) && !vm.IsLoading)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyDiff(vm));
        };

        // If already loaded
        if (!vm.IsLoading)
            ApplyDiff(vm);
    }

    private void ApplyDiff(GitDiffDocumentViewModel vm)
    {
        if (_leftEditor == null || _rightEditor == null) return;

        // Set syntax highlighting based on file extension
        var ext = Path.GetExtension(vm.FilePath).ToLowerInvariant();
        var highlighting = GetHighlighting(ext);

        _leftEditor.Editor.SyntaxHighlighting  = highlighting;
        _rightEditor.Editor.SyntaxHighlighting = highlighting;

        // Set text
        _leftEditor.Editor.Text  = vm.LeftText;
        _rightEditor.Editor.Text = vm.RightText;

        // Add diff background renderers
        _leftEditor.TextArea.TextView.BackgroundRenderers.Clear();
        _leftEditor.TextArea.TextView.BackgroundRenderers.Add(
            new DiffBackgroundRenderer(_leftEditor.Editor, vm.LeftLines));

        _rightEditor.TextArea.TextView.BackgroundRenderers.Clear();
        _rightEditor.TextArea.TextView.BackgroundRenderers.Add(
            new DiffBackgroundRenderer(_rightEditor.Editor, vm.RightLines));

        _leftEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        _rightEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);

        SetupSyncScrolling(_leftEditor.Editor, _rightEditor.Editor);
    }

    private bool _isSyncScrollingSetup = false;
    private void SetupSyncScrolling(TextEditor left, TextEditor right)
    {
        if (_isSyncScrollingSetup) return;

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

    private static IHighlightingDefinition? GetHighlighting(string ext) => ext switch
    {
        ".cs"               => HighlightingManager.Instance.GetDefinitionByExtension(".cs"),
        ".xaml" or ".axaml" => HighlightingManager.Instance.GetDefinitionByExtension(".xml"),
        ".xml"              => HighlightingManager.Instance.GetDefinitionByExtension(".xml"),
        ".json"             => HighlightingManager.Instance.GetDefinitionByExtension(".json"),
        ".js" or ".ts"      => HighlightingManager.Instance.GetDefinitionByExtension(".js"),
        ".css"              => HighlightingManager.Instance.GetDefinitionByExtension(".css"),
        _                   => null
    };
}

