using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Extensions;
using System;
using System.Collections.Generic;

namespace Scadix.AxamlDesigner.Extensions;

/// <summary>Split resize and move handles edit source once on release; the designer model stays unchanged.</summary>
[ExtensionFor(typeof(Control))]
[ExtensionServer(typeof(SplitModeExtensionServer))]
public sealed class SplitResizeThumbExtension : DefaultExtension
{
    private readonly List<(Border Handle, int X, int Y)> _resizeHandles = new();
    private Border? _moveHandle;
    private Border? _borderDrag;
    private Canvas? _overlay;
    private Control? _view;
    private ISplitResizeOverlayService? _service;
    private Func<double?, double?, bool>? _resizeCommit;
    private Func<double, double, bool>? _moveCommit;
    private IPointer? _pointer;
    private Point _start;
    private Size _oldSize, _size;
    private int _resizeX, _resizeY;
    private Point _oldPos;
    private bool _isMoving;
    private bool _removed;

    public bool IsResizing => _pointer != null && !_isMoving;
    public bool IsMoving => _pointer != null && _isMoving;

    protected override void OnInitialized()
    {
        _service = Services.GetService<ISplitResizeOverlayService>();
        _overlay = _service?.OverlayCanvas;
        _view = ExtendedItem.View as Control;
        if (_overlay == null || _view == null) return;

        var resizeCommit = _service?.CreateResizeCommit(ExtendedItem);
        var moveCommit = _service?.CreateMoveCommit(ExtendedItem);

        if (resizeCommit != null)
        {
            _resizeCommit = resizeCommit;
            AddResizeHandle("TopLeft", -1, -1, StandardCursorType.TopLeftCorner);
            AddResizeHandle("Top", 0, -1, StandardCursorType.TopSide);
            AddResizeHandle("TopRight", 1, -1, StandardCursorType.TopRightCorner);
            AddResizeHandle("Left", -1, 0, StandardCursorType.LeftSide);
            AddResizeHandle("Right", 1, 0, StandardCursorType.RightSide);
            AddResizeHandle("BottomLeft", -1, 1, StandardCursorType.BottomLeftCorner);
            AddResizeHandle("Bottom", 0, 1, StandardCursorType.BottomSide);
            AddResizeHandle("BottomRight", 1, 1, StandardCursorType.BottomRightCorner);
        }

        if (moveCommit != null)
        {
            _moveCommit = moveCommit;
            AddMoveHandle();
            AddBorderDrag();
        }

        if (_resizeCommit != null || _moveCommit != null)
        {
            _overlay.LayoutUpdated += LayoutUpdated;
            UpdatePositions();
        }
    }

    private void AddResizeHandle(string name, int x, int y, StandardCursorType cursor)
    {
        var handle = new Border
        {
            Name = "SplitResize" + name, Width = 8, Height = 8,
            Background = Brushes.White, BorderBrush = Brushes.DodgerBlue,
            BorderThickness = new Thickness(1), Cursor = new Cursor(cursor), Focusable = true
        };
        _resizeHandles.Add((handle, x, y));
        _overlay!.Children.Add(handle);
        handle.PointerPressed += (_, e) =>
        {
            if (_removed || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed || _pointer != null) return;
            _resizeCommit = _service?.CreateResizeCommit(ExtendedItem);
            if (_resizeCommit == null) return;
            _resizeX = x; _resizeY = y;
            _start = e.GetPosition(_view);
            _oldSize = _size = _view!.Bounds.Size;
            _pointer = e.Pointer;
            _isMoving = false;
            e.Pointer.Capture(handle);
            handle.Focus();
            e.Handled = true;
        };
        handle.PointerMoved += (_, e) =>
        {
            if (_pointer != e.Pointer || _isMoving) return;
            var delta = e.GetPosition(_view) - _start;
            double w = _oldSize.Width + delta.X * _resizeX, h = _oldSize.Height + delta.Y * _resizeY;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && _resizeX != 0 && _resizeY != 0 && _oldSize.Width > 0 && _oldSize.Height > 0)
            {
                var scale = Math.Abs(delta.X / _oldSize.Width) >= Math.Abs(delta.Y / _oldSize.Height)
                    ? w / _oldSize.Width : h / _oldSize.Height;
                var min = Math.Max(_view!.MinWidth / _oldSize.Width, _view.MinHeight / _oldSize.Height);
                var max = Math.Min(_view.MaxWidth / _oldSize.Width, _view.MaxHeight / _oldSize.Height);
                scale = Math.Clamp(scale, min, Math.Max(min, max));
                w = _oldSize.Width * scale; h = _oldSize.Height * scale;
            }
            else
            {
                w = Math.Clamp(w, _view!.MinWidth, Math.Max(_view.MinWidth, _view.MaxWidth));
                h = Math.Clamp(h, _view.MinHeight, Math.Max(_view.MinHeight, _view.MaxHeight));
            }
            _size = new Size(w, h);
            UpdatePositions();
            e.Handled = true;
        };
        handle.PointerReleased += (_, e) =>
        {
            if (_pointer != e.Pointer || _isMoving) return;
            var commit = _resizeCommit;
            var size = _size;
            var old = _oldSize;
            EndDrag();
            if (size != old) commit?.Invoke(_resizeX == 0 ? null : size.Width, _resizeY == 0 ? null : size.Height);
            e.Handled = true;
        };
        handle.PointerCaptureLost += (_, _) => EndDrag();
        handle.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && IsResizing) { EndDrag(); e.Handled = true; }
        };
    }

    private void AddMoveHandle()
    {
        _moveHandle = new Border
        {
            Name = "SplitMoveHandle", Width = 12, Height = 12,
            Background = Brushes.DodgerBlue, BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2), Cursor = new Cursor(StandardCursorType.SizeAll), Focusable = true,
            CornerRadius = new CornerRadius(6)
        };
        _overlay!.Children.Add(_moveHandle);
        _moveHandle.PointerPressed += (_, e) =>
        {
            if (_removed || !e.GetCurrentPoint(_moveHandle).Properties.IsLeftButtonPressed || _pointer != null) return;
            _moveCommit = _service?.CreateMoveCommit(ExtendedItem);
            if (_moveCommit == null) return;
            _start = e.GetPosition(_view);
            _oldPos = new Point(0, 0);
            _pointer = e.Pointer;
            _isMoving = true;
            e.Pointer.Capture(_moveHandle);
            _moveHandle.Focus();
            e.Handled = true;
        };
        _moveHandle.PointerMoved += (_, e) =>
        {
            if (_pointer != e.Pointer || !_isMoving) return;
            var delta = e.GetPosition(_view) - _start;
            _oldPos = new Point(delta.X, delta.Y);
            UpdatePositions();
            e.Handled = true;
        };
        _moveHandle.PointerReleased += (_, e) =>
        {
            if (_pointer != e.Pointer || !_isMoving) return;
            var commit = _moveCommit;
            var pos = _oldPos;
            EndDrag();
            CommitMove(commit, pos);
            e.Handled = true;
        };
        _moveHandle.PointerCaptureLost += (_, _) => EndDrag();
        _moveHandle.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && IsMoving) { EndDrag(); e.Handled = true; }
        };
    }

    private void AddBorderDrag()
    {
        _borderDrag = new Border
        {
            Name = "SplitBorderDrag",
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 30, 144, 255)),
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.SizeAll),
            IsHitTestVisible = true, Focusable = true
        };
        _overlay!.Children.Insert(0, _borderDrag);
        _borderDrag.PointerPressed += (_, e) =>
        {
            if (_removed || !e.GetCurrentPoint(_borderDrag).Properties.IsLeftButtonPressed || _pointer != null) return;
            _moveCommit = _service?.CreateMoveCommit(ExtendedItem);
            if (_moveCommit == null) return;
            _start = e.GetPosition(_view);
            _oldPos = new Point(0, 0);
            _pointer = e.Pointer;
            _isMoving = true;
            e.Pointer.Capture(_borderDrag);
            _borderDrag.Focus();
            e.Handled = true;
        };
        _borderDrag.PointerMoved += (_, e) =>
        {
            if (_pointer != e.Pointer || !_isMoving) return;
            var delta = e.GetPosition(_view) - _start;
            _oldPos = new Point(delta.X, delta.Y);
            UpdatePositions();
            e.Handled = true;
        };
        _borderDrag.PointerReleased += (_, e) =>
        {
            if (_pointer != e.Pointer || !_isMoving) return;
            var commit = _moveCommit;
            var pos = _oldPos;
            EndDrag();
            CommitMove(commit, pos);
            e.Handled = true;
        };
        _borderDrag.PointerCaptureLost += (_, _) => EndDrag();
        _borderDrag.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && IsMoving) { EndDrag(); e.Handled = true; }
        };
    }

    private void CommitMove(Func<double, double, bool>? commit, Point delta)
    {
        if (delta == default || _view?.GetVisualParent() is not Visual parent) return;
        var transform = _view.TransformToVisual(parent);
        if (!transform.HasValue) return;
        var displacement = transform.Value.Transform(delta) - transform.Value.Transform(default(Point));
        commit?.Invoke(displacement.X, displacement.Y);
    }

    private void EndDrag()
    {
        var pointer = _pointer;
        _pointer = null;
        _isMoving = false;
        _resizeCommit = null;
        _moveCommit = null;
        pointer?.Capture(null);
        UpdatePositions();
    }

    private void LayoutUpdated(object? sender, EventArgs e) => UpdatePositions();

    private void UpdatePositions()
    {
        if (_removed || _view == null || _overlay == null) return;
        var transform = _view.TransformToVisual(_overlay);
        if (!transform.HasValue) return;
        var size = IsResizing ? _size : _view.Bounds.Size;
        var offset = IsMoving ? transform.Value.Transform(_oldPos) - transform.Value.Transform(default(Point)) : default(Vector);

        foreach (var (handle, x, y) in _resizeHandles)
        {
            var point = transform.Value.Transform(new Point((x + 1) * size.Width / 2, (y + 1) * size.Height / 2));
            point += offset;
            Canvas.SetLeft(handle, point.X - 4);
            Canvas.SetTop(handle, point.Y - 4);
        }

        if (_moveHandle != null)
        {
            var centerPoint = transform.Value.Transform(new Point(size.Width / 2, size.Height / 2));
            if (IsMoving)
            {
                centerPoint += offset;
            }
            Canvas.SetLeft(_moveHandle, centerPoint.X - 6);
            Canvas.SetTop(_moveHandle, centerPoint.Y - 6);
        }

        if (_borderDrag != null)
        {
            var bounds = new Rect(size).TransformToAABB(transform.Value);
            Canvas.SetLeft(_borderDrag, bounds.X + offset.X);
            Canvas.SetTop(_borderDrag, bounds.Y + offset.Y);
            _borderDrag.Width = bounds.Width;
            _borderDrag.Height = bounds.Height;
        }
    }

    protected override void OnRemove()
    {
        _removed = true;
        EndDrag();
        if (_overlay != null)
        {
            _overlay.LayoutUpdated -= LayoutUpdated;
            foreach (var entry in _resizeHandles) _overlay.Children.Remove(entry.Handle);
            if (_moveHandle != null) _overlay.Children.Remove(_moveHandle);
            if (_borderDrag != null) _overlay.Children.Remove(_borderDrag);
        }
        _resizeHandles.Clear();
    }
}
