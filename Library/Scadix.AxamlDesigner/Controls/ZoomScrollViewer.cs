 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Globalization;
using Scadix.AxamlDesign.UIExtensions;

namespace Scadix.AxamlDesigner.Controls;
public class ZoomScrollViewer : ScrollViewer
{
    protected override Type StyleKeyOverride => typeof(ZoomScrollViewer);

    public static readonly StyledProperty<bool> EnableHorizontalWheelSupportProperty =
        AvaloniaProperty.Register<ZoomScrollViewer, bool>(nameof(EnableHorizontalWheelSupport), false);

    public static readonly StyledProperty<double> CurrentZoomProperty =
        AvaloniaProperty.Register<ZoomScrollViewer, double>(nameof(CurrentZoom), 1.0, coerce: CoerceZoom);

    public static readonly StyledProperty<double> MinimumZoomProperty =
        AvaloniaProperty.Register<ZoomScrollViewer, double>(nameof(MinimumZoom), 0.2);

    public static readonly StyledProperty<double> MaximumZoomProperty =
        AvaloniaProperty.Register<ZoomScrollViewer, double>(nameof(MaximumZoom), 5.0);

    public static readonly StyledProperty<bool> MouseWheelZoomProperty =
        AvaloniaProperty.Register<ZoomScrollViewer, bool>(nameof(MouseWheelZoom), true);

    public static readonly StyledProperty<bool> AlwaysShowZoomButtonsProperty =
        AvaloniaProperty.Register<ZoomScrollViewer, bool>(nameof(AlwaysShowZoomButtons), false);

    public static readonly StyledProperty<bool> IsRulerVisibleProperty =
        AvaloniaProperty.Register<ZoomScrollViewer, bool>(nameof(IsRulerVisible), false);

    public static readonly DirectProperty<ZoomScrollViewer, bool> ComputedZoomButtonCollapsedProperty =
        AvaloniaProperty.RegisterDirect<ZoomScrollViewer, bool>(
            nameof(ComputedZoomButtonCollapsed),
            o => o.ComputedZoomButtonCollapsed);

    private bool _computedZoomButtonCollapsed = true;

    static ZoomScrollViewer()
    {
        CurrentZoomProperty.Changed.AddClassHandler<ZoomScrollViewer>((x, e) => x.CalculateZoomButtonCollapsed());
        AlwaysShowZoomButtonsProperty.Changed.AddClassHandler<ZoomScrollViewer>((x, e) => x.CalculateZoomButtonCollapsed());
    }

    public bool EnableHorizontalWheelSupport
    {
        get => GetValue(EnableHorizontalWheelSupportProperty);
        set => SetValue(EnableHorizontalWheelSupportProperty, value);
    }

    public double CurrentZoom
    {
        get => GetValue(CurrentZoomProperty);
        set => SetValue(CurrentZoomProperty, value);
    }

    public double MinimumZoom
    {
        get => GetValue(MinimumZoomProperty);
        set => SetValue(MinimumZoomProperty, value);
    }

    public double MaximumZoom
    {
        get => GetValue(MaximumZoomProperty);
        set => SetValue(MaximumZoomProperty, value);
    }

    public bool MouseWheelZoom
    {
        get => GetValue(MouseWheelZoomProperty);
        set => SetValue(MouseWheelZoomProperty, value);
    }

    public bool AlwaysShowZoomButtons
    {
        get => GetValue(AlwaysShowZoomButtonsProperty);
        set => SetValue(AlwaysShowZoomButtonsProperty, value);
    }

    public bool IsRulerVisible
    {
        get => GetValue(IsRulerVisibleProperty);
        set => SetValue(IsRulerVisibleProperty, value);
    }

    public bool ComputedZoomButtonCollapsed
    {
        get => _computedZoomButtonCollapsed;
        private set => SetAndRaise(ComputedZoomButtonCollapsedProperty, ref _computedZoomButtonCollapsed, value);
    }

    private static double CoerceZoom(AvaloniaObject sender, double baseValue)
    {
        var sv = (ZoomScrollViewer)sender;
        return Math.Max(sv.MinimumZoom, Math.Min(sv.MaximumZoom, baseValue));
    }

    private void CalculateZoomButtonCollapsed()
    {
        ComputedZoomButtonCollapsed = !AlwaysShowZoomButtons && CurrentZoom == 1.0;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (!e.Handled && e.KeyModifiers == KeyModifiers.Control && MouseWheelZoom)
        {
            var oldZoom = CurrentZoom;
            var newZoom = RoundToOneIfClose(CurrentZoom * Math.Pow(1.001, e.Delta.Y * 120));
            newZoom = Math.Max(MinimumZoom, Math.Min(MaximumZoom, newZoom));

            // adjust scroll position so that mouse stays over the same virtual coordinate
            var presenter = this.FindDescendantOfType<ContentPresenter>();
            Vector relMousePos;
            if (presenter != null)
            {
                var mousePos = e.GetPosition(presenter);
                relMousePos = new Vector(mousePos.X / presenter.Bounds.Width, mousePos.Y / presenter.Bounds.Height);
            }
            else
            {
                relMousePos = new Vector(0.5, 0.5);
            }

            var scrollOffset = new Point(Offset.X, Offset.Y);
            var oldHalfViewport = new Vector(Viewport.Width / 2, Viewport.Height / 2);
            var newHalfViewport = oldHalfViewport / newZoom * oldZoom;
            var oldCenter = scrollOffset + oldHalfViewport;
            var virtualMousePos =
                scrollOffset + new Vector(relMousePos.X * Viewport.Width, relMousePos.Y * Viewport.Height);

            var f = Math.Min(newZoom, oldZoom) / Math.Max(newZoom, oldZoom);
            var lambda = 1 - f * f;
            if (oldZoom > newZoom)
                lambda = -lambda;

            var newCenter = oldCenter + lambda * (virtualMousePos - oldCenter);
            scrollOffset = newCenter - newHalfViewport;

            CurrentZoom = newZoom;

            Offset = new Vector(scrollOffset.X, scrollOffset.Y);

            e.Handled = true;
        }

        base.OnPointerWheelChanged(e);
    }

    internal static double RoundToOneIfClose(double val)
    {
        if (Math.Abs(val - 1.0) < 0.001)
            return 1.0;
        return val;
    }

    public void ZoomToFit()
    {
        var presenter = this.FindDescendantOfType<ContentPresenter>();
        if (presenter?.Child is Visual visualContent)
        {
            var contentSize = visualContent.Bounds.Size;
            if (contentSize.Width > 0 && contentSize.Height > 0)
            {
                double zoomX = Viewport.Width / contentSize.Width;
                double zoomY = Viewport.Height / contentSize.Height;
                CurrentZoom = Math.Min(zoomX, zoomY) * 0.9; // 10% margin
                Offset = new Vector(0, 0);
            }
        }
    }

    public void ZoomToWidth()
    {
        var presenter = this.FindDescendantOfType<ContentPresenter>();
        if (presenter?.Child is Visual visualContent)
        {
            var contentSize = visualContent.Bounds.Size;
            if (contentSize.Width > 0)
            {
                CurrentZoom = (Viewport.Width / contentSize.Width) * 0.9; // 10% margin
                Offset = new Vector(0, Offset.Y);
            }
        }
    }
}

internal sealed class IsNormalZoomConverter : IValueConverter
{
    public static readonly IsNormalZoomConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is bool && (bool)parameter)
            return true;
        return (double)value == 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}