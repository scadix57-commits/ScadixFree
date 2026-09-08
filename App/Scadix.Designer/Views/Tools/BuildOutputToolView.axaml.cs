using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Scadix.Designer.Services;
using System.Collections.Specialized;

namespace Scadix.Designer.Views.Tools;

public partial class BuildOutputToolView : UserControl
{
    private ListBox? _listBox;
    private ScrollViewer? _scrollViewer;

    public BuildOutputToolView()
    {
        InitializeComponent();

        _listBox = this.FindControl<ListBox>("uxLogsListBox");

        if (_listBox != null)
        {
            _listBox.TemplateApplied += (sender, e) =>
            {
                _scrollViewer = e.NameScope.Find<ScrollViewer>("uxScrollViewer");
            };

            ((INotifyCollectionChanged)_listBox.Items).CollectionChanged += (sender, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    _scrollViewer?.ScrollToEnd();
                }
            };
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OutputSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Logic to switch output source can be added here
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        BuildOutputService.Instance.Clear();
    }
}
