using System;
using System.Collections.Generic;
using System.ComponentModel;

using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Editing;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using System.Xml.Linq;
using System.Text;

using Scadix.AxamlDesigner.Services;
using Scadix.Designer.Services;
using Scadix.Designer.Views.Tools;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Editing;
using Avalonia.Controls.Primitives;
using Scadix.Designer.Controls.Completion;
using System.Linq;
using Scadix.Designer.ViewModels.Tools;
using Avalonia.Interactivity;

namespace Scadix.Designer;

/// <summary>
/// Rich XAML/CS editor built on AvaloniaEdit with full debugger integration.
///
/// Debugger features (same as MyDesigner_Master):
///   - BreakpointMargin  : red circles + yellow arrow + hover ghost
///   - ExecutionLineHighlighter : yellow background on current execution line
///   - BreakpointService : central store, synced on toggle and on file open
///   - DebugToolbarViewModel : ExecutionLineMoved / ExecutionLineCleared events
///   - F9 : toggle breakpoint at caret
/// </summary>
public partial class XamlEditorView : UserControl
{
    private TextEditor?                _editor;
    private FoldingManager?            _foldingManager;
    private XmlFoldingStrategy?        _xmlFoldingStrategy;
    private readonly BraceFoldingStrategy? _braceFoldingStrategy;
    private bool _isXmlFolding;
    private DispatcherTimer?           _foldingTimer;

    private Document?                  _document;
    private bool                       _updatingEditor;
    private BreakpointMargin?          _breakpointMargin;
    private ExecutionLineHighlighter?  _executionHighlighter;
    private bool                       _isWaitingForChord;

    // ── Completion System ───────────────────────────────────────────────────
    private XamlCompletionWindow? _csCompletion;

    // ── Debugger VM (singleton) ───────────────────────────────────────────
    private readonly DebugToolbarViewModel _debugVm = DebugToolbarViewModel.Instance;

    public XamlEditorView()
    {
        InitializeComponent();

        // Get CompletionWindow from XAML (same approach as CSharpEditor)
        _csCompletion = this.FindControl<XamlCompletionWindow>("CompletionWindow");

        // Hide completion on any pointer press anywhere in this view (Tunnel = fires first)
        this.AddHandler(
            InputElement.PointerPressedEvent,
            (object? s, PointerPressedEventArgs e) =>
            {
                // Only hide if click is NOT inside the completion window itself
                if (_csCompletion?.IsVisible == true)
                {
                    var pos = e.GetPosition(_csCompletion);
                    bool insideWindow = pos.X >= 0 && pos.Y >= 0 &&
                                        pos.X <= _csCompletion.Bounds.Width &&
                                        pos.Y <= _csCompletion.Bounds.Height;
                    if (!insideWindow)
                        _csCompletion.IsVisible = false;
                }
            },
            RoutingStrategies.Tunnel);

        // ── Execution line events ─────────────────────────────────────────
        _debugVm.ExecutionLineMoved   += OnExecutionLineMoved;
        _debugVm.ExecutionLineCleared += OnExecutionLineCleared;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_editor == null)
            CreateEditor();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _debugVm.ExecutionLineMoved   -= OnExecutionLineMoved;
        _debugVm.ExecutionLineCleared -= OnExecutionLineCleared;
        UnsubscribeDocument();
        _foldingTimer?.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    // ── Execution line events ─────────────────────────────────────────────

    private void OnExecutionLineMoved(object? sender, (string File, int Line) e)
    {
        if (_document?.FilePath == null || _breakpointMargin == null) return;

        // Match .cs file to .axaml.cs or direct match
        bool isMatch =
            string.Equals(_document.FilePath, e.File, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_document.FilePath + ".cs", e.File, StringComparison.OrdinalIgnoreCase);

        if (isMatch)
            SetCurrentExecutionLine(e.Line);
        else
            SetCurrentExecutionLine(-1);
    }

    private void OnExecutionLineCleared(object? sender, EventArgs e)
    {
        SetCurrentExecutionLine(-1);
    }

    public void SetCurrentExecutionLine(int line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _breakpointMargin?.SetCurrentExecutionLine(line);
            _executionHighlighter?.SetLine(line);
            _editor?.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        });
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Attach to a Document — called from DocumentView.Loaded.</summary>
    public void AttachDocument(Document doc)
    {
        UnsubscribeDocument();
        _document = doc;

        if (_editor != null)
        {
            _editor.SyntaxHighlighting = GetHighlighting(doc.FilePath);

            // ── Switch folding strategy ──────────────────────────────────
            var ext = System.IO.Path.GetExtension(doc.FilePath ?? "").ToLowerInvariant();
            _isXmlFolding = ext is ".xaml" or ".axaml" or ".csproj" or ".slnx" or ".xml" or ".sln";
            _updatingEditor = true;
            _editor.Text    = doc.Text;
            _updatingEditor = false;
            UpdateFolding();


            // ── Restore breakpoints from BreakpointService ────────────────
            if (_breakpointMargin != null && !string.IsNullOrEmpty(doc.FilePath))
            {
                var existing = BreakpointService.Instance.GetBreakpoints(doc.FilePath);
                _breakpointMargin.SetBreakpoints(existing);
            }

            // ── If already paused on this file, show execution arrow ───────
            if (_debugVm.IsPaused &&
                string.Equals(_debugVm.CurrentFile, doc.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                _breakpointMargin?.SetCurrentExecutionLine(_debugVm.CurrentLine);
                _executionHighlighter?.SetLine(_debugVm.CurrentLine);
            }
        }

        doc.PropertyChanged += OnDocumentPropertyChanged;
    }

    /// <summary>Expose the inner TextArea for context-menu command parameters.</summary>
    public TextArea? TextArea => _editor?.TextArea;

    /// <summary>Expose the inner TextEditor for settings application.</summary>
    public TextEditor? Editor => _editor;

    /// <summary>Expose the breakpoint margin for DocumentView.SetCurrentExecutionLine.</summary>
    public BreakpointMargin? BreakpointMargin => _breakpointMargin;

    /// <summary>Jump to a XAML error position.</summary>
    public void JumpToError(XamlError error)
    {
        if (_editor == null) return;
        try
        {
            _editor.ScrollTo(error.Line, error.Column);
            _editor.CaretOffset = _editor.Document.GetOffset(error.Line, error.Column);

            int n = 0;
            while (_editor.CaretOffset + n < _editor.Document.TextLength)
            {
                char c = _editor.Document.GetCharAt(_editor.CaretOffset + n);
                if (c is ' ' or '.' or '<' or '>' or '"') break;
                n++;
            }
            _editor.SelectionLength = n;
        }
        catch { /* invalid position */ }
    }

    /// <summary>Format the current XAML document using standard indentation.</summary>
    public void FormatDocument()
    {
        if (_editor == null || string.IsNullOrWhiteSpace(_editor.Text)) return;
        
        try
        {
            var doc = XDocument.Parse(_editor.Text);
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = _editor.Options.ConvertTabsToSpaces 
                    ? new string(' ', _editor.Options.IndentationSize) 
                    : "\t",
                NewLineChars = "\n",
                OmitXmlDeclaration = true
            };

            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, settings))
            {
                doc.Save(writer);
            }

            _updatingEditor = true;
            _editor.Text = sb.ToString();
            _updatingEditor = false;
            UpdateFolding();
            
            if (_document != null) _document.Text = _editor.Text;
        }
        catch (Exception ex)
        {
            BuildOutputService.Instance.AppendLine($"[Editor] Format failed: {ex.Message}");
        }
    }


    public void UncommentSelection()
    {
        if (_editor == null) return;
        var selection = _editor.TextArea.Selection;
        if (selection.IsEmpty) return;

        string text = selection.GetText();
        if (text.StartsWith("<!--") && text.EndsWith("-->"))
        {
            _editor.Document.Replace(selection.SurroundingSegment, text.Substring(4, text.Length - 7));
        }
    }

    public void CommentSelection()
    {
        if (_editor == null) return;
        var selection = _editor.TextArea.Selection;
        if (selection.IsEmpty) return;
        
        _editor.Document.Replace(selection.SurroundingSegment, "<!--" + selection.GetText() + "-->");
    }

    public void DuplicateLine()
    {
        if (_editor == null) return;
        var selection = _editor.TextArea.Selection;
        if (selection.IsEmpty)
        {
            var line = _editor.Document.GetLineByOffset(_editor.CaretOffset);
            _editor.Document.Insert(line.EndOffset, Environment.NewLine + _editor.Document.GetText(line));
        }
        else
        {
            _editor.Document.Insert(selection.SurroundingSegment.EndOffset, selection.GetText());
        }
    }



    private void CreateEditor()
    {
        var s = Scadix.Designer.Settings.Default;

        double fontSize = s.EditorFontSizeIndex switch
        {
            0 => 10, 1 => 11, 2 => 12, 3 => 13, 4 => 14, 5 => 16, _ => 12
        };

        _editor = new TextEditor
        {
            FontFamily      = new FontFamily(s.EditorFontFamily),
            FontSize        = fontSize,
            Background      = new SolidColorBrush(Color.Parse("#FFFFFF")),
            Foreground      = new SolidColorBrush(Color.Parse("#000000")),
            ShowLineNumbers = s.EditorShowLineNumbers,
            WordWrap        = false,
            SyntaxHighlighting = null,
            Options = new TextEditorOptions
            {
                ConvertTabsToSpaces         = !s.EditorUseTabCharacter,
                IndentationSize             = s.EditorIndentSize,
                ShowBoxForControlCharacters = false,
                HighlightCurrentLine        = s.EditorHighlightCurrentLine,
                EnableHyperlinks            = true,
                EnableEmailHyperlinks       = true,
                AllowScrollBelowDocument    = true,
                EnableRectangularSelection  = true,
                ShowTabs                    = s.EditorShowWhitespace,
                ShowSpaces                  = s.EditorShowWhitespace,
            },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
        };

        _editor.LineNumbersForeground = new SolidColorBrush(Color.Parse("#999999"));

        // ── Background renderers ──────────────────────────────────────────
        _editor.TextArea.TextView.BackgroundRenderers.Add(
            new CurrentLineHighlightRenderer(_editor));
        _editor.TextArea.TextView.BackgroundRenderers.Add(
            new IndentationGuideRenderer(_editor));
        _editor.TextArea.TextView.BackgroundRenderers.Add(
            new BracketHighlightRenderer(_editor));
        
        // ── Error Squiggles Renderer ──────────────────────────────────────
        _editor.TextArea.TextView.BackgroundRenderers.Add(
            new ErrorSquiggleRenderer(_editor));


        
        // ── Enable Search Panel (Ctrl+F) ──────────────────────────────────
        SearchPanel.Install(_editor);



        // ── Execution line highlighter (yellow background) ────────────────
        _executionHighlighter = new ExecutionLineHighlighter();
        _editor.TextArea.TextView.BackgroundRenderers.Add(_executionHighlighter);

        // ── Breakpoint margin (always shown — matches MyDesigner_Master) ──
        _breakpointMargin = new BreakpointMargin();
        _editor.TextArea.LeftMargins.Insert(0, _breakpointMargin);

        // Toggle → sync with BreakpointService
        bool _syncingFromService = false;

        _breakpointMargin.BreakpointToggled += (_, line) =>
        {
            if (_syncingFromService) return;   
            if (_document?.FilePath != null)
            {
                BreakpointService.Instance.ToggleBreakpoint(_document.FilePath, line);
                BuildOutputService.Instance.AppendLine(
                    $"[Breakpoint] Toggled line {line} in {System.IO.Path.GetFileName(_document.FilePath)}");
            }
        };

        // BreakpointService → sync margin when changed from outside (e.g. Breakpoints panel)
        BreakpointService.Instance.BreakpointsChanged += (_, e) =>
        {
            if (_document?.FilePath == null) return;
            if (!string.Equals(e.FilePath, _document.FilePath, StringComparison.OrdinalIgnoreCase)) return;

            Dispatcher.UIThread.Post(() =>
            {
                _syncingFromService = true;
                try   { _breakpointMargin.SetBreakpoints(e.Breakpoints); }
                finally { _syncingFromService = false; }
            });
        };

        // ── F9 key → toggle breakpoint at caret ───────────────────────────
        _editor.TextArea.KeyDown += (_, e) =>
        {
            // Handle completion window navigation (same as CSharpEditor)
            if (_csCompletion?.IsVisible == true)
            {
                if (e.Key == Key.Down)
                {
                    _ = _csCompletion.MoveSelectionAsync(1);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Up)
                {
                    _ = _csCompletion.MoveSelectionAsync(-1);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.PageDown)
                {
                    _ = _csCompletion.MoveSelectionAsync((int)(_csCompletion.Height - 33) / 20);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.PageUp)
                {
                    _ = _csCompletion.MoveSelectionAsync(-(int)(_csCompletion.Height - 33) / 20);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    _csCompletion.Commit();
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Escape)
                {
                    _csCompletion.IsVisible = false;
                    e.Handled = true;
                    return;
                }
            }
            
            if (e.Key == Key.F9 && e.KeyModifiers == KeyModifiers.None)
            {
                _breakpointMargin.ToggleBreakpointAtCaret();
                e.Handled = true;
            }
            
            // ── Shortcuts: Ctrl+... ───────────────────────────────────────
            if (e.KeyModifiers == KeyModifiers.Control)
            {
                if (e.Key == Key.D && !_isWaitingForChord) { DuplicateLine(); e.Handled = true; }
                else if (e.Key == Key.K) _isWaitingForChord = true;
                else if (_isWaitingForChord)
                {
                    if (e.Key == Key.D) { FormatDocument(); e.Handled = true; }
                    else if (e.Key == Key.C) { CommentSelection(); e.Handled = true; }
                    else if (e.Key == Key.U) { UncommentSelection(); e.Handled = true; }
                    _isWaitingForChord = false;
                }
                else _isWaitingForChord = false;
            }
            else _isWaitingForChord = false;

        };


        // ── TextEntering → Commit on special chars (same as CSharpEditor) ──
        _editor.TextArea.TextEntering += OnTextEntering;
        
        // ── TextEntered → Auto-close tags & Completion ────────────────────
        _editor.TextArea.TextEntered += OnTextEntered;
        
        // Hide completion window on focus lost (same as CSharpEditor)
        _editor.LostFocus += (_, _) =>
        {
            if (_csCompletion != null)
                _csCompletion.IsVisible = false;
        };
        
        // Hide completion window on pointer press — use Tunnel to catch BEFORE TextArea
        // This fires for any click anywhere in the editor area
        _editor.TextArea.AddHandler(
            InputElement.PointerPressedEvent,
            (object? s, PointerPressedEventArgs e) =>
            {
                if (_csCompletion != null)
                    _csCompletion.IsVisible = false;
            },
            RoutingStrategies.Tunnel);
        
        // Use Tunneling to catch arrow keys before TextArea consumes them
        _editor.TextArea.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        
        // Disable default rectangular selection to match original editor if needed
        _editor.TextArea.Caret.PositionChanged += (s, e) => 
        {
            _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        };

        // ── Folding setup ────────────────────────────────────────────────
        _foldingManager       = FoldingManager.Install(_editor.TextArea);
        _xmlFoldingStrategy   = new XmlFoldingStrategy();

        _foldingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };

        _foldingTimer.Tick += (_, _) => { _foldingTimer.Stop(); UpdateFolding(); };

        _editor.TextChanged += OnEditorTextChanged;

        // Mount into ContentControl placeholder
        var host = this.FindControl<ContentControl>("EditorHost");
        if (host != null) host.Content = _editor;

        // Sync if document was attached before editor was created
        if (_document != null)
        {
            _updatingEditor = true;
            _editor.Text    = _document.Text;
            _updatingEditor = false;
            UpdateFolding();
        }
    }

    // ── Syntax highlighting ───────────────────────────────────────────────

    private static IHighlightingDefinition? GetHighlighting(string? filePath)
    {
        if (filePath == null) return LoadXamlHighlighting();

        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".xaml" or ".axaml" or ".csproj" or ".slnx" or ".sln" => LoadXamlHighlighting(),
            ".cs"               => HighlightingManager.Instance.GetDefinitionByExtension(".cs"),
            ".fs"               => HighlightingManager.Instance.GetDefinitionByExtension(".fs"),
            ".vb"               => HighlightingManager.Instance.GetDefinitionByExtension(".vb"),
            ".json"             => HighlightingManager.Instance.GetDefinitionByExtension(".json"),
            ".xml"              => HighlightingManager.Instance.GetDefinitionByExtension(".xml"),
            ".html" or ".htm"   => HighlightingManager.Instance.GetDefinitionByExtension(".html"),
            ".css"              => HighlightingManager.Instance.GetDefinitionByExtension(".css"),
            ".js" or ".ts"      => HighlightingManager.Instance.GetDefinitionByExtension(".js"),
            _                   => null
        };
    }

    private static IHighlightingDefinition? LoadXamlHighlighting()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(
                "Scadix.Designer.Assets.XamlHighlighting.xshd");
            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }
        catch { }
        return HighlightingManager.Instance.GetDefinitionByExtension(".xml");
    }

    // ── Sync: editor → document ───────────────────────────────────────────

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_updatingEditor || _document == null || _editor == null) return;
        _document.Text = _editor.Text;
        _foldingTimer?.Stop();
        _foldingTimer?.Start();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // ── 1. If Completion is open, forward keys to it ───────────────────
        if (_csCompletion?.IsVisible == true)
        {
            if (e.Key == Key.Down)
            {
                e.Handled = true;
                await _csCompletion.MoveSelectionAsync(1);
                return;
            }
            if (e.Key == Key.Up)
            {
                e.Handled = true;
                await _csCompletion.MoveSelectionAsync(-1);
                return;
            }
            if (e.Key == Key.PageDown)
            {
                e.Handled = true;
                await _csCompletion.MoveSelectionAsync(10);
                return;
            }
            if (e.Key == Key.PageUp)
            {
                e.Handled = true;
                await _csCompletion.MoveSelectionAsync(-10);
                return;
            }
            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                e.Handled = true;
                _csCompletion.Commit();
                return;
            }
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _csCompletion.IsVisible = false;
                return;
            }
            if (e.Key == Key.Back)
            {
                // After backspace, update filter
                _ = Dispatcher.UIThread.InvokeAsync(async () => {
                    await UpdateFilterAsync();
                }, DispatcherPriority.Input);
            }
        }

        // ── 2. Open IntelliSense on Ctrl+Space ─────────────────────────────
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            await TriggerCsCompletionAsync();
        }
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        // مثل CSharpEditor.OnTextEntering — يُعالج قبل إدخال الحرف
        if (_csCompletion?.IsVisible == true && e.Text?.Length >= 1)
        {
            char inserting = e.Text[0];
            // أحرف التأكيد: Space, >, /, =, Enter
            if (inserting is ' ' or '>' or '/' or '=' or '\n' or '\r')
            {
                _csCompletion.Commit();
                // لا نمنع الحرف — يُدخل في المحرر بشكل طبيعي
            }
        }
    }

    private async void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (_editor == null) return;

        // ── 1. Auto-close XML tags ────────────────────────────────────────
        if (e.Text == ">")
        {
            HandleAutoCloseTag();
        }

        // ── 2. Trigger IntelliSense ───────────────────────────────────────
        if (e.Text == "<" || e.Text == " " || e.Text == "." || e.Text == "=" || e.Text == "{")
        {
            await TriggerCsCompletionAsync();
        }
        else if (_csCompletion?.IsVisible == true)
        {
            // Filter as user types
            await UpdateFilterAsync();
        }
    }

    private async Task UpdateFilterAsync()
    {
        if (_csCompletion == null || !_csCompletion.IsVisible || _editor == null) return;

        int caretOff = _editor.CaretOffset;
        int start    = caretOff;
        while (start > 0 && IsWordChar(_editor.Document.GetCharAt(start - 1))) start--;

        string filter = _editor.Document.GetText(start, caretOff - start);
        await _csCompletion.SetFilterTextAsync(filter);
        
        if (_csCompletion.VisibleItems == 0)
            _csCompletion.IsVisible = false;
    }

    private void HandleAutoCloseTag()
    {
        if (_editor == null) return;
        var caretOffset = _editor.CaretOffset;
        if (caretOffset < 2) return;

        if (_editor.Document.GetCharAt(caretOffset - 2) == '/') return;

        int start = caretOffset - 2;
        while (start > 0 && _editor.Document.GetCharAt(start) != '<')
        {
            char c = _editor.Document.GetCharAt(start);
            if (c == '>' || c == '/') return;
            start--;
        }
    }

    private async Task TriggerCsCompletionAsync()
    {
        if (_editor == null) return;

        string textUpToCaret = _editor.Document.GetText(0, _editor.CaretOffset);
        string extension     = System.IO.Path.GetExtension(_document?.FilePath ?? "").ToLowerInvariant();
        if (extension != ".xaml" && extension != ".axaml") return;

        var context = XamlCompletionEngine.DetectContext(textUpToCaret, extension);
        if (context == XamlCompletionContext.None) return;

        var items = XamlCompletionEngine.GetCompletions(textUpToCaret, context, extension);
        if (items.Count == 0) return;

        if (_csCompletion == null) return;

        // Setup Committed event handler (only once)
        if (_csCompletion != null)
        {
            _csCompletion.Committed -= OnCompletionCommitted; // Remove old handler
            _csCompletion.Committed += OnCompletionCommitted; // Add new handler
        }

        await _csCompletion.SetItemsAsync(items);
        if (_csCompletion.VisibleItems == 0) return;

        // Position the completion window (same approach as CSharpEditor)
        var caretRect = _editor.TextArea.Caret.CalculateCaretRectangle();
        _csCompletion.PositionAt(caretRect, _editor.TextArea);
        _csCompletion.IsVisible = true;
    }

    private void OnCompletionCommitted(object? sender, XamlCompletionItem item)
    {
        if (_editor == null || _csCompletion == null) return;
        
        int caretOff = _editor.CaretOffset;
        int start    = caretOff;
        while (start > 0 && IsWordChar(_editor.Document.GetCharAt(start - 1))) start--;
        
        item.Complete(_editor.TextArea,
            new AvaloniaEdit.Document.SimpleSegment(start, caretOff - start),
            EventArgs.Empty);
        
        _csCompletion.IsVisible = false;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.';

    // ── Sync: document → editor ───────────────────────────────────────────

    private async void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_document == null || _editor == null) return;

        if (e.PropertyName == nameof(Document.Text) && _document.Text != _editor.Text)
        {
            _updatingEditor = true;
            _editor.Text    = _document.Text;
            _updatingEditor = false;
            UpdateFolding();
        }

        if (e.PropertyName == nameof(Document.XamlElementLineInfo))
        {
            try
            {
                await Task.Delay(70);
                if (_document.XamlElementLineInfo != null)
                {
                    _editor.SelectionLength = 0;
                    _editor.SelectionStart  = _document.XamlElementLineInfo.Position;
                    _editor.SelectionLength = _document.XamlElementLineInfo.Length;
                }
                else
                {
                    _editor.SelectionStart  = 0;
                    _editor.SelectionLength = 0;
                }
                _editor.Focus();
            }
            catch { }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void UpdateFolding()
    {
        if (_foldingManager == null || _editor == null) return;
        try 
        { 
            if (_isXmlFolding)
                _xmlFoldingStrategy?.UpdateFoldings(_foldingManager, _editor.Document);
            else
                _braceFoldingStrategy?.UpdateFoldings(_foldingManager, _editor.Document);
        }
        catch { }
    }


    private void UnsubscribeDocument()
    {
        if (_document == null) return;
        _document.PropertyChanged -= OnDocumentPropertyChanged;
        _document = null;
    }
}

// ── Current-line highlight ────────────────────────────────────────────────────

public class CurrentLineHighlightRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    public KnownLayer Layer => KnownLayer.Background;

    public CurrentLineHighlightRenderer(TextEditor editor) => _editor = editor;

    public void Draw(TextView textView, DrawingContext ctx)
    {
        if (!_editor.TextArea.Selection.IsEmpty) return;

        var line = _editor.Document.GetLineByOffset(_editor.CaretOffset);
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, line))
        {
            ctx.DrawRectangle(
                new SolidColorBrush(Color.Parse("#E8F4FF")),
                null,
                new Rect(0, rect.Y, textView.Bounds.Width, rect.Height));
        }
    }
}

// ── Indentation guides ────────────────────────────────────────────────────────

public class IndentationGuideRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    public KnownLayer Layer => KnownLayer.Background;

    public IndentationGuideRenderer(TextEditor editor) => _editor = editor;

    public void Draw(TextView textView, DrawingContext ctx)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse("#D8D8D8")), 1)
        {
            DashStyle = new DashStyle(new double[] { 1, 3 }, 0)
        };

        int    indentSize = _editor.Options.IndentationSize;
        double charWidth  = textView.WideSpaceWidth;

        foreach (var vl in textView.VisualLines)
        {
            var docLine = vl.FirstDocumentLine;
            var text    = _editor.Document.GetText(docLine.Offset, docLine.Length);

            int indent = 0;
            foreach (char c in text)
            {
                if (c == ' ') indent++;
                else break;
            }

            for (int i = indentSize; i < indent; i += indentSize)
            {
                double x  = i * charWidth;
                double y1 = vl.VisualTop - textView.ScrollOffset.Y;
                double y2 = y1 + vl.Height;
                ctx.DrawLine(pen, new Point(x, y1), new Point(x, y2));
            }
        }
    }
}

// ── Bracket highlighting ──────────────────────────────────────────────────────

public class BracketHighlightRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    public KnownLayer Layer => KnownLayer.Selection;

    public BracketHighlightRenderer(TextEditor editor) => _editor = editor;

    public void Draw(TextView textView, DrawingContext ctx)
    {
        if (_editor.CaretOffset <= 0 || _editor.CaretOffset > _editor.Document.TextLength) return;

        char c = _editor.Document.GetCharAt(_editor.CaretOffset - 1);
        int matchingOffset = -1;

        if (c == '<') matchingOffset = FindMatchingBracket(_editor.Document, _editor.CaretOffset - 1, '<', '>');
        else if (c == '>') matchingOffset = FindMatchingBracket(_editor.Document, _editor.CaretOffset - 1, '>', '<');
        else if (c == '{') matchingOffset = FindMatchingBracket(_editor.Document, _editor.CaretOffset - 1, '{', '}');
        else if (c == '}') matchingOffset = FindMatchingBracket(_editor.Document, _editor.CaretOffset - 1, '}', '{');

        if (matchingOffset != -1)
        {
            HighlightBracket(textView, ctx, _editor.CaretOffset - 1);
            HighlightBracket(textView, ctx, matchingOffset);
        }
    }

    private void HighlightBracket(TextView textView, DrawingContext ctx, int offset)
    {
        var line = _editor.Document.GetLineByOffset(offset);
        var segment = new AvaloniaEdit.Document.TextSegment { StartOffset = offset, Length = 1 };
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            ctx.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(Color.Parse("#0078D4")), 1),
                rect.Inflate(0.5));
        }
    }

    private int FindMatchingBracket(AvaloniaEdit.Document.TextDocument doc, int offset, char open, char close)
    {
        int depth = 1;
        int dir = (open == '<' || open == '{') ? 1 : -1;
        int i = offset + dir;

        while (i >= 0 && i < doc.TextLength)
        {
            char c = doc.GetCharAt(i);
            if (c == open) depth++;
            else if (c == close) depth--;

            if (depth == 0) return i;
            i += dir;
        }
        return -1;
    }
}


// ── Error squiggles ───────────────────────────────────────────────────────────

public class ErrorSquiggleRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    public KnownLayer Layer => KnownLayer.Selection;

    public ErrorSquiggleRenderer(TextEditor editor) => _editor = editor;

    public void Draw(TextView textView, DrawingContext ctx)
    {
        if (_editor.Document == null) return;
    }
}

// ── Brace folding strategy for C# ───────────────────────────────────────────

public class BraceFoldingStrategy
{
    public void UpdateFoldings(FoldingManager manager, AvaloniaEdit.Document.TextDocument document)
    {
        var newFoldings = CreateNewFoldings(document, out int firstErrorOffset);
        manager.UpdateFoldings(newFoldings, firstErrorOffset);
    }

    public IEnumerable<NewFolding> CreateNewFoldings(AvaloniaEdit.Document.TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var list = new List<NewFolding>();
        var startOffsets = new Stack<int>();

        for (int i = 0; i < document.TextLength; i++)
        {
            char c = document.GetCharAt(i);
            if (c == '{') startOffsets.Push(i);
            else if (c == '}' && startOffsets.Count > 0)
            {
                int start = startOffsets.Pop();
                list.Add(new NewFolding(start, i + 1));
            }
        }
        list.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return list;
    }
}





