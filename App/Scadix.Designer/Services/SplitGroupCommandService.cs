using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Extensions;

namespace Scadix.Designer.Services;

/// <summary>Applies a discrete group command through the same source batch used by Split gestures.</summary>
public sealed class SplitGroupCommandService : ISplitGroupCommandService, IDisposable
{
    private readonly ISelectionService _selection;
    private readonly ISplitResizeOverlayService _overlay;
    private readonly Action _refresh;
    private bool _disposed;

    public SplitGroupCommandService(ISelectionService selection, ISplitResizeOverlayService overlay, Action refresh)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
    }

    public bool CanAlign => CreateEligibleGroupSnapshot(2) != null;
    public bool CanDistribute => CreateEligibleGroupSnapshot(3) != null;

    public bool Align(GroupAlignment alignment)
    {
        var snapshot = CreateEligibleGroupSnapshot(2);
        return snapshot != null && Commit(snapshot,
            SplitGroupGeometry.Align(snapshot.Bounds, snapshot.PrimaryIndex, alignment));
    }

    public bool Distribute(GroupDistribution direction)
    {
        var snapshot = CreateEligibleGroupSnapshot(3);
        return snapshot != null && Commit(snapshot, SplitGroupGeometry.Distribute(snapshot.Bounds, direction));
    }

    private Snapshot? CreateEligibleGroupSnapshot(int minimumCount)
    {
        if (_disposed || _selection.SelectionCount < minimumCount) return null;
        var items = _selection.SelectedItems.ToArray();
        var primaryIndex = Array.IndexOf(items, _selection.PrimarySelection);
        if (primaryIndex < 0 || items[0].Parent is not { View: Panel parent } parentItem
            || parent is not (Canvas or Grid)) return null;
        var bounds = new Rect[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].Parent != parentItem || items[i].View is not Control control
                || control.GetVisualParent() != parent) return null;
            var bound = control.Bounds;
            if (!double.IsFinite(bound.X) || !double.IsFinite(bound.Y)
                || !double.IsFinite(bound.Right) || !double.IsFinite(bound.Bottom)
                || !double.IsFinite(bound.Width) || !double.IsFinite(bound.Height)
                || bound.Width < 0 || bound.Height < 0) return null;
            bounds[i] = bound;
        }
        var commit = _overlay.CreateGroupCommit(items, includeSize: false);
        return commit == null ? null : new Snapshot(bounds, primaryIndex, commit);
    }

    private bool Commit(Snapshot snapshot, IReadOnlyList<Rect> bounds)
    {
        // Avoid source normalization and a preview reload when geometry is already correct.
        if (snapshot.Bounds.SequenceEqual(bounds) || !snapshot.Commit(bounds)) return false;
        _refresh();
        return true;
    }

    public void Dispose() => _disposed = true;

    private sealed record Snapshot(IReadOnlyList<Rect> Bounds, int PrimaryIndex, Func<IReadOnlyList<Rect>, bool> Commit);
}
