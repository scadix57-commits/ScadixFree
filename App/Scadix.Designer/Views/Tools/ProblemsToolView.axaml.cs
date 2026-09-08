using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Collections;
using System.Collections.Specialized;
using Scadix.AxamlDesigner.Services;

namespace Scadix.Designer;

public partial class ProblemsToolView : UserControl
{
    // ── ItemsSource StyledProperty ───────────────────────────────────────

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ProblemsToolView, IEnumerable?>(nameof(ItemsSource));

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ProblemsToolView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) =>
          DataContext = this.FindAncestorOfType<Window>()?.DataContext;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ItemsSourceProperty) return;

        // Unsubscribe from old collection
        if (change.OldValue is INotifyCollectionChanged oldCol)
            oldCol.CollectionChanged -= OnCollectionChanged;

        // Subscribe to new collection
        if (change.NewValue is INotifyCollectionChanged newCol)
            newCol.CollectionChanged += OnCollectionChanged;

        SyncItems();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => SyncItems();

    private void SyncItems()
    {
        var items = this.FindControl<ItemsControl>("PART_Items");
        if (items != null)
            items.ItemsSource = ItemsSource;

        // Update error count badge
        var countText = this.FindControl<TextBlock>("ErrorCountText");
        if (countText != null)
        {
            int count = 0;
            if (ItemsSource != null)
                foreach (var _ in ItemsSource) count++;
            countText.Text = $"{count} Error{(count != 1 ? "s" : "")}";
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            var error = (e.Source as Control)?.DataContext as XamlError;
            if (error != null)
                MainWindowViewModel.Instance.JumpToError(error);
        }
    }
}
