using System.ComponentModel;
using Avalonia.Controls;
using Scadix.AxamlDesign;
using Scadix.Designer.ViewModels.Tools;

namespace Scadix.Designer.Views.Tools;

/// <summary>
/// LogicalTreeView — نفس نهج PropertiesToolView
/// يستمع لتغيير CurrentDocument ويستدعي SetRoot عند تحميل المصمم
/// </summary>
public partial class LogicalTreeView : UserControl
{
    private Document? _currentDoc;

    public LogicalTreeView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttached;

        // DataContext قد يتغير بعد الإنشاء — نستمع له أيضاً
        DataContextChanged += (_, _) =>
        {
            // عند تعيين DataContext، ادفع المستند الحالي فوراً
            if (_currentDoc != null)
                PushRoot(_currentDoc);
        };
    }

    private void OnAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        MainWindowViewModel.Instance.PropertyChanged += OnShellPropertyChanged;
        SwitchDocument(MainWindowViewModel.Instance.CurrentDocument);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        MainWindowViewModel.Instance.PropertyChanged -= OnShellPropertyChanged;
        UnsubscribeDocument();
        base.OnDetachedFromVisualTree(e);
    }

    // ── Shell.CurrentDocument changed ─────────────────────────────────────

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentDocument))
            SwitchDocument(MainWindowViewModel.Instance.CurrentDocument);
    }

    private void SwitchDocument(Document? doc)
    {
        UnsubscribeDocument();
        _currentDoc = doc;

        if (doc == null)
        {
            GetViewModel()?.Clear();
            return;
        }

        // استمع لتغييرات المستند لالتقاط OutlineRoot بعد UpdateDesign
        doc.PropertyChanged += OnDocumentPropertyChanged;

        // ادفع فوراً إذا كان المصمم محملاً بالفعل
        PushRoot(doc);
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // OutlineRoot يُطلق بعد UpdateDesign() — هذا هو الوقت المناسب لتحديث الشجرة
        if (e.PropertyName == "OutlineRoot" || e.PropertyName == "SelectionService")
        {
            if (_currentDoc != null)
                PushRoot(_currentDoc);
        }
    }

    private void UnsubscribeDocument()
    {
        if (_currentDoc != null)
        {
            _currentDoc.PropertyChanged -= OnDocumentPropertyChanged;
            _currentDoc = null;
        }
    }

    // ── Push root to ViewModel ────────────────────────────────────────────

    private void PushRoot(Document doc)
    {
        var vm = GetViewModel();
        if (vm == null) return;

        // RootItem متاح دائماً بعد UpdateDesign حتى في Xaml mode
        var root      = doc.DesignContext?.RootItem;
        var selection = doc.SelectionService; // null في Xaml mode — مقبول

        vm.SetRoot(root, selection);
    }

    private LogicalTreeViewModel? GetViewModel()
        => DataContext as LogicalTreeViewModel;
}
