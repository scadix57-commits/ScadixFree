using Dock.Model.Mvvm.Controls;
namespace Scadix.Designer.ViewModels;

public class DocumentViewModel : Dock.Model.Mvvm.Controls.Document
{
    private Document? _document;

    public Document? Document
    {
        get => _document;
        set { _document = value; OnPropertyChanged(); }
    }

    public DocumentViewModel(Document doc)
    {
        _document = doc;
        Id = doc.Name;
        Title = doc.Title;
    }

    public override bool OnClose()
    {
        if (_document != null)
        {
            // Run async close with save confirmation dialog
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                await MainWindowViewModel.Instance.CloseAsync(_document);
            });
        }
        return base.OnClose();
    }
}
