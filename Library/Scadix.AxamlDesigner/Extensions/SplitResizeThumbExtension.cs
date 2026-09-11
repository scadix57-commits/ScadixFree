using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesign.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scadix.AxamlDesigner.Extensions;

/// <summary>Split resize and move handles edit source once on release; the designer model stays unchanged.</summary>
[ExtensionFor(typeof(Control))]
[ExtensionServer(typeof(SplitModeExtensionServer))]
public sealed class SplitResizeThumbExtension : DefaultExtension
{
    private readonly List<(Border Handle, int X, int Y)> _resizeHandles = new();
    private readonly List<LineGuide> _lastAlignmentGuides = new();
    private Border? _moveHandle;
    private Border? _borderDrag;
    private Canvas? _overlay;
    private Control? _view;
    private ISplitResizeOverlayService? _service;
    private ISelectionService? _selectionService;
    private Func<double?, double?, double?, double?, bool>? _resizeCommit;
    private Func<double, double, bool>? _moveCommit;
    private IPointer? _pointer;
    private Point _start;
    private Point _oldBoundsPosition;
    private Size _oldSize, _size;
    private int _resizeX, _resizeY;
    private Point _oldPos;
    private bool _isMoving;
    private bool _removed;
    private double _posDeltaX, _posDeltaY;

    public bool IsResizing => _pointer != null && !_isMoving;
    public bool IsMoving => _pointer != null && _isMoving;

    protected override void OnInitialized()
    {
        _service = Services.GetService<ISplitResizeOverlayService>();
        _selectionService = Services.GetService<ISelectionService>();
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
            _overlay.KeyDown += OverlayKeyDown;
        }

        if (_resizeCommit != null || _moveCommit != null)
        {
            _overlay.LayoutUpdated += LayoutUpdated;
            UpdatePositions();
        }
    }

    private static Point Snap(Point p, double grid, bool enabled, KeyModifiers modifiers)
    {
        if (!enabled || grid <= 0 || modifiers.HasFlag(KeyModifiers.Alt)) return p;
        return new Point(Math.Round(p.X / grid) * grid, Math.Round(p.Y / grid) * grid);
    }

    private static Size Snap(Size s, double grid, bool enabled, KeyModifiers modifiers)
    {
        if (!enabled || grid <= 0 || modifiers.HasFlag(KeyModifiers.Alt)) return s;
        return new Size(Math.Round(s.Width / grid) * grid, Math.Round(s.Height / grid) * grid);
    }

    private Point SnapMoveDelta(Vector delta, double grid, bool enabled, KeyModifiers modifiers)
    {
        var target = Snap(_oldBoundsPosition + delta, grid, enabled, modifiers);
        return target - _oldBoundsPosition;
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
            _posDeltaX = 0; _posDeltaY = 0;
            _start = e.GetPosition(_view);
            _oldBoundsPosition = _view.Bounds.Position;
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

            // Position adjustment for left/top edge resize (VS-style)
            _posDeltaX = 0; _posDeltaY = 0;

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

            // Snap only dimensions controlled by this handle. For proportional corner
            // resizing, snap the dominant dimension and derive the other from the ratio.
            var grid = _service?.SnapGridSize ?? 8;
            var snapEnabled = _service?.SnapEnabled ?? true;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && _resizeX != 0 && _resizeY != 0 && _oldSize.Width > 0 && _oldSize.Height > 0)
            {
                var ratio = _oldSize.Width / _oldSize.Height;
                if (Math.Abs(delta.X / _oldSize.Width) >= Math.Abs(delta.Y / _oldSize.Height))
                {
                    w = Snap(new Size(w, h), grid, snapEnabled, e.KeyModifiers).Width;
                    h = w / ratio;
                }
                else
                {
                    h = Snap(new Size(w, h), grid, snapEnabled, e.KeyModifiers).Height;
                    w = h * ratio;
                }
            }
            else
            {
                var snappedSize = Snap(new Size(w, h), grid, snapEnabled, e.KeyModifiers);
                if (_resizeX != 0) w = snappedSize.Width;
                if (_resizeY != 0) h = snappedSize.Height;
            }

// Use the final constrained size, including proportional resizing.
            _posDeltaX = _resizeX == -1 ? _oldSize.Width - w : 0;
            _posDeltaY = _resizeY == -1 ? _oldSize.Height - h : 0;
            _size = new Size(w, h);
            UpdatePositions();

            // Update alignment guides
            var transform = _view!.TransformToVisual(_overlay!);
            if (transform.HasValue)
            {
                var movingBounds = new Rect(_size).TransformToAABB(transform.Value);
                var localOffset = new Point(_posDeltaX, _posDeltaY);
                var offset = transform.Value.Transform(localOffset) - transform.Value.Transform(default(Point));
                movingBounds = new Rect(movingBounds.Position + offset, movingBounds.Size);
                UpdateAlignmentGuides(movingBounds, e.KeyModifiers);
            }

            // Show snap readout
            _service?.ShowSnapReadout(e.GetPosition(_overlay), _size);
            e.Handled = true;
        };
        handle.PointerReleased += (_, e) =>
        {
            if (_pointer != e.Pointer || _isMoving) return;
            var commit = _resizeCommit;
            var size = _size;
            var old = _oldSize;
            var posX = _posDeltaX;
            var posY = _posDeltaY;
            EndDrag();
            _service?.HideSnapReadout();
            if (size != old || posX != 0 || posY != 0)
            {
                _overlay!.Focus();
                commit?.Invoke(_resizeX == 0 ? null : size.Width, _resizeY == 0 ? null : size.Height, _resizeX == -1 ? posX : null, _resizeY == -1 ? posY : null);
            }
            e.Handled = true;
        };
        handle.PointerCaptureLost += (_, _) => { EndDrag(); _service?.HideSnapReadout(); };
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
            _oldBoundsPosition = _view.Bounds.Position;
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

            // Apply snap to position
            var grid = _service?.SnapGridSize ?? 8;
            var snapEnabled = _service?.SnapEnabled ?? true;
            _oldPos = SnapMoveDelta(delta, grid, snapEnabled, e.KeyModifiers);

            UpdatePositions();

            // Update alignment guides
            var movingBounds = GetBoundsInOverlay(_view!);
            if (movingBounds.Width > 0 && movingBounds.Height > 0)
            {
                var transform = _view!.TransformToVisual(_overlay!);
                if (transform.HasValue)
                {
                    var offset = transform.Value.Transform(_oldPos) - transform.Value.Transform(default(Point));
                    movingBounds = new Rect(movingBounds.X + offset.X, movingBounds.Y + offset.Y, movingBounds.Width, movingBounds.Height);
                    UpdateAlignmentGuides(movingBounds, e.KeyModifiers);
                }
            }

            // Show snap readout
            _service?.ShowSnapReadout(_oldBoundsPosition + (Vector)_oldPos);
            e.Handled = true;
        };
        _moveHandle.PointerReleased += (_, e) =>
        {
            if (_pointer != e.Pointer || !_isMoving) return;
            var commit = _moveCommit;
            var pos = _oldPos;
            EndDrag();
            _service?.HideSnapReadout();
            CommitMove(commit, pos);
            e.Handled = true;
        };
        _moveHandle.PointerCaptureLost += (_, _) => { EndDrag(); _service?.HideSnapReadout(); };
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
            _oldBoundsPosition = _view.Bounds.Position;
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

            // Apply snap to position
            var grid = _service?.SnapGridSize ?? 8;
            var snapEnabled = _service?.SnapEnabled ?? true;
            _oldPos = SnapMoveDelta(delta, grid, snapEnabled, e.KeyModifiers);

            UpdatePositions();

            // Update alignment guides
            var movingBounds = GetBoundsInOverlay(_view!);
            if (movingBounds.Width > 0 && movingBounds.Height > 0)
            {
                var transform = _view!.TransformToVisual(_overlay!);
                if (transform.HasValue)
                {
                    var offset = transform.Value.Transform(_oldPos) - transform.Value.Transform(default(Point));
                    movingBounds = new Rect(movingBounds.X + offset.X, movingBounds.Y + offset.Y, movingBounds.Width, movingBounds.Height);
                    UpdateAlignmentGuides(movingBounds, e.KeyModifiers);
                }
            }

            // Show snap readout
            _service?.ShowSnapReadout(_oldBoundsPosition + (Vector)_oldPos);
            e.Handled = true;
        };
        _borderDrag.PointerReleased += (_, e) =>
        {
            if (_pointer != e.Pointer || !_isMoving) return;
            var commit = _moveCommit;
            var pos = _oldPos;
            EndDrag();
            _service?.HideSnapReadout();
            CommitMove(commit, pos);
            e.Handled = true;
        };
        _borderDrag.PointerCaptureLost += (_, _) => { EndDrag(); _service?.HideSnapReadout(); };
        _borderDrag.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && IsMoving) { EndDrag(); e.Handled = true; }
        };
    }

    private void OverlayKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _removed || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;

        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10.0 : 1.0;
        double deltaX = 0, deltaY = 0;

        switch (e.Key)
        {
            case Key.Left: deltaX = -step; break;
            case Key.Right: deltaX = step; break;
            case Key.Up: deltaY = -step; break;
            case Key.Down: deltaY = step; break;
            default: return;
        }
        // A drag owns its transaction until release or Escape.
        e.Handled = true;
        if (_pointer != null) return;
        var commit = _service?.CreateMoveCommit(ExtendedItem);
        if (commit == null) return;
        // The overlay survives preview reloads, unlike the selected control's handles.
        _overlay!.Focus();
        if (_view?.GetVisualParent() is Visual parent)
        {
            var parentTransform = parent.TransformToVisual(_overlay);
            if (parentTransform.HasValue)
            {
                var displacement = parentTransform.Value.Transform(new Point(deltaX, deltaY))
                    - parentTransform.Value.Transform(default(Point));
                var currentBounds = GetBoundsInOverlay(_view);
                UpdateAlignmentGuides(new Rect(currentBounds.Position + displacement, currentBounds.Size), e.KeyModifiers);
            }
        }
        var keyboardGuides = _lastAlignmentGuides.ToArray();
        if (commit(deltaX, deltaY))
        {
            _service?.RefreshAfterKeyboardEdit();
            if (keyboardGuides.Length > 0) _service?.ShowAlignmentGuides(keyboardGuides);
        }
    }

    private void CommitMove(Func<double, double, bool>? commit, Point delta)
    {
        if (delta == default || _view?.GetVisualParent() is not Visual parent) return;
        var transform = _view.TransformToVisual(parent);
        if (!transform.HasValue) return;
        var displacement = transform.Value.Transform(delta) - transform.Value.Transform(default(Point));
        _overlay!.Focus();
        commit?.Invoke(displacement.X, displacement.Y);
    }

    private void EndDrag()
    {
        var pointer = _pointer;
        _pointer = null;
        _isMoving = false;
        _resizeCommit = null;
        _moveCommit = null;
        _posDeltaX = 0; _posDeltaY = 0;
        pointer?.Capture(null);
        _service?.HideAlignmentGuides();
        _lastAlignmentGuides.Clear();
        UpdatePositions();
    }

    private void UpdateAlignmentGuides(Rect movingBounds, KeyModifiers modifiers)
    {
        if (_service == null || _overlay == null || _view == null || _selectionService == null) return;

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            _service.HideAlignmentGuides();
            return;
        }

        var threshold = _service.AlignmentGuideThreshold;
        if (threshold <= 0)
        {
            _service.HideAlignmentGuides();
            return;
        }

        var designRoot = _service.DesignRoot;
        if (designRoot == null) return;

        // Get all selected items (including the one being dragged)
        var selectedItems = _selectionService.SelectedItems.OfType<DesignItem>().ToList();
        if (selectedItems.Count == 0) return;

        var activeView = _selectionService.PrimarySelection?.View as Control ?? _view;
        var currentPrimaryBounds = GetBoundsInOverlay(activeView);
        Rect unionBounds;
        if (selectedItems.Count == 1)
        {
            unionBounds = movingBounds;
        }
        else if (IsMoving)
        {
            var currentUnion = ComputeUnionBounds(selectedItems);
            var displacement = movingBounds.Position - currentPrimaryBounds.Position;
            unionBounds = new Rect(currentUnion.Position + displacement, currentUnion.Size);
        }
        else
        {
            var otherBounds = ComputeUnionBounds(selectedItems.Where(item => item.View != activeView));
            unionBounds = otherBounds.Width > 0 && otherBounds.Height > 0
                ? otherBounds.Union(movingBounds)
                : movingBounds;
        }
        if (unionBounds.Width <= 0 || unionBounds.Height <= 0) return;

        // Find sibling controls in the same parent panel
        var selectedViews = selectedItems.Select(item => item.View).ToHashSet();
        var parent = activeView.GetVisualParent() as Panel;
        if (parent == null) return;

        var siblings = parent.Children
            .OfType<Control>()
            .Where(c => !selectedViews.Contains(c) && c.IsVisible && c.Bounds.Width > 0 && c.Bounds.Height > 0)
            .Select(c => new { Control = c, Bounds = GetBoundsInOverlay(c) })
            .Where(x => x.Bounds.Width > 0 && x.Bounds.Height > 0)
            .ToList();

        if (siblings.Count == 0)
        {
            _service.HideAlignmentGuides();
            return;
        }

        var guides = new List<LineGuide>();
        var extent = _service.AlignmentGuideExtent;
        var overlaySize = new Size(_overlay.Bounds.Width, _overlay.Bounds.Height);

        // Edges and centers of moving control(s)
        var movingLeft = unionBounds.Left;
        var movingRight = unionBounds.Right;
        var movingCenterX = unionBounds.Center.X;
        var movingTop = unionBounds.Top;
        var movingBottom = unionBounds.Bottom;
        var movingCenterY = unionBounds.Center.Y;

        foreach (var sibling in siblings)
        {
            var s = sibling.Bounds;
            var sLeft = s.Left;
            var sRight = s.Right;
            var sCenterX = s.Center.X;
            var sTop = s.Top;
            var sBottom = s.Bottom;
            var sCenterY = s.Center.Y;

            // Vertical guides (align left/center/right edges)
            CheckAndAddGuide(guides, Orientation.Vertical, movingLeft, sLeft, movingTop, movingBottom, sTop, sBottom, threshold, extent, overlaySize, modifiers);
            CheckAndAddGuide(guides, Orientation.Vertical, movingCenterX, sCenterX, movingTop, movingBottom, sTop, sBottom, threshold, extent, overlaySize, modifiers);
            CheckAndAddGuide(guides, Orientation.Vertical, movingRight, sRight, movingTop, movingBottom, sTop, sBottom, threshold, extent, overlaySize, modifiers);

            // Horizontal guides (align top/center/bottom edges)
            CheckAndAddGuide(guides, Orientation.Horizontal, movingTop, sTop, movingLeft, movingRight, sLeft, sRight, threshold, extent, overlaySize, modifiers);
            CheckAndAddGuide(guides, Orientation.Horizontal, movingCenterY, sCenterY, movingLeft, movingRight, sLeft, sRight, threshold, extent, overlaySize, modifiers);
            CheckAndAddGuide(guides, Orientation.Horizontal, movingBottom, sBottom, movingLeft, movingRight, sLeft, sRight, threshold, extent, overlaySize, modifiers);
        }

        if (guides.Count > 0)
        {
            _lastAlignmentGuides.Clear();
            _lastAlignmentGuides.AddRange(guides);
            _service.ShowAlignmentGuides(guides);
        }
        else
        {
            _lastAlignmentGuides.Clear();
            _service.HideAlignmentGuides();
        }
    }

    private Rect ComputeUnionBounds(IEnumerable<DesignItem> items)
    {
        if (_overlay == null) return new Rect();

        var first = true;
        var union = new Rect();

        foreach (var item in items)
        {
            if (item.View is not Control control) continue;
            var bounds = GetBoundsInOverlay(control);
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;

            if (first)
            {
                union = bounds;
                first = false;
            }
            else
            {
                union = union.Union(bounds);
            }
        }

        return union;
    }

    private Rect GetBoundsInOverlay(Control control)
    {
        if (_overlay == null) return new Rect();
        var transform = control.TransformToVisual(_overlay);
        if (!transform.HasValue) return new Rect();
        return new Rect(control.Bounds.Size).TransformToAABB(transform.Value);
    }

    private void CheckAndAddGuide(
        List<LineGuide> guides,
        Orientation orientation,
        double movingPos, double siblingPos,
        double movingStart, double movingEnd,
        double siblingStart, double siblingEnd,
        double threshold,
        GuideExtent extent,
        Size overlaySize,
        KeyModifiers modifiers)
    {
        if (Math.Abs(movingPos - siblingPos) > threshold) return;

        double start, end;
        if (extent == GuideExtent.BetweenControls)
        {
            start = Math.Min(movingStart, siblingStart);
            end = Math.Max(movingEnd, siblingEnd);
        }
        else // FullSurface
        {
            start = orientation == Orientation.Vertical ? 0 : 0;
            end = orientation == Orientation.Vertical ? overlaySize.Height : overlaySize.Width;
        }

        guides.Add(new LineGuide(orientation, siblingPos, start, end));
    }

    private void LayoutUpdated(object? sender, EventArgs e) => UpdatePositions();

    private void UpdatePositions()
    {
        if (_removed || _view == null || _overlay == null) return;
        var transform = _view.TransformToVisual(_overlay);
        if (!transform.HasValue) return;
        var size = IsResizing ? _size : _view.Bounds.Size;
        var localOffset = IsMoving ? _oldPos : IsResizing ? new Point(_posDeltaX, _posDeltaY) : default;
        var offset = transform.Value.Transform(localOffset) - transform.Value.Transform(default(Point));

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
            centerPoint += offset;
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
            _overlay.KeyDown -= OverlayKeyDown;
            foreach (var entry in _resizeHandles) _overlay.Children.Remove(entry.Handle);
            if (_moveHandle != null) _overlay.Children.Remove(_moveHandle);
            if (_borderDrag != null) _overlay.Children.Remove(_borderDrag);
        }
        _resizeHandles.Clear();
    }
}
