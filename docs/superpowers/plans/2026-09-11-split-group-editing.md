# Split View Group Editing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add source-preserving multi-selection movement, proportional resizing, alignment, and equal-gap distribution to Split mode.

**Architecture:** Keep pointer interaction in `SplitResizeThumbExtension`, isolate union-bound math in a pure geometry helper, and persist final item rectangles through one atomic source-edit callback owned by `DocumentView` and `SplitPropertyEditorFactory`. Group commands use the same geometry and persistence path, so gestures and commands share validation, formatting preservation, preview refresh, and Undo/Redo behavior.

**Tech Stack:** C# 13, .NET 10, Avalonia 11.3.13, AvaloniaEdit `TextDocument`, existing Scadix design services and console integration-test harness.

**Spec:** `docs/superpowers/specs/2026-09-11-split-group-editing-design.md`

## Global Constraints

- Editable selections contain at least two sibling controls with the same direct Canvas or Grid parent.
- Group resize proportionally transforms every child rectangle around the opposite union edge or corner.
- Canvas source edits use `Canvas.Left`, `Canvas.Top`, `Width`, and `Height`; Grid edits use alignment-aware `Margin` and explicit dimensions for resize.
- Bindings, resource references, unresolved source spans, and stale document revisions reject the entire batch.
- Every gesture or command creates exactly one Undo/Redo entry; Escape writes nothing.
- Preserve quote style, spacing, indentation, comments, and all unrelated XAML bytes.
- Existing single-selection behavior and tests remain unchanged.

---

### Task 1: Pure Group Geometry

**Files:**
- Create: `Library/Scadix.AxamlDesign/SplitGroupEditing.cs`
- Create: `Library/Scadix.AxamlDesigner/Extensions/SplitGroupGeometry.cs`
- Create: `tests/SplitViewIntegration/SplitGroupGeometryChecks.cs`
- Modify: `tests/SplitViewIntegration/Program.cs:45-70`

**Interfaces:**
- Consumes: Avalonia `Rect` and `Vector`.
- Produces: `SplitGroupGeometry.Union`, `Translate`, `Scale`, `Align`, and `Distribute`.

- [ ] **Step 1: Add failing geometry checks and a command-line entry point**

```csharp
var items = new[] { new Rect(10, 20, 20, 10), new Rect(50, 40, 10, 20) };
Check(SplitGroupGeometry.Union(items) == new Rect(10, 20, 50, 40), "Union encloses every item");
Check(SplitGroupGeometry.Translate(items, new Vector(8, -2))[1] == new Rect(58, 38, 10, 20), "Translate preserves relative geometry");
var scaled = SplitGroupGeometry.Scale(items, new Rect(10, 20, 100, 80));
Check(scaled[0] == new Rect(10, 20, 40, 20) && scaled[1] == new Rect(90, 60, 20, 40), "Scale is proportional");
Check(SplitGroupGeometry.Align(items, 1, GroupAlignment.Left)[0].X == 50, "Align uses primary item");
```

Add a three-item horizontal and vertical distribution case that verifies equal gaps and unchanged outer items.

- [ ] **Step 2: Run the focused checks and verify RED**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-geometry-only
```

Expected: compilation fails because the group geometry types do not exist.

- [ ] **Step 3: Implement the minimal pure geometry API**

```csharp
// SplitGroupEditing.cs, namespace Scadix.AxamlDesign
public enum GroupAlignment { Left, HorizontalCenter, Right, Top, VerticalCenter, Bottom }
public enum GroupDistribution { Horizontal, Vertical }

// SplitGroupGeometry.cs, namespace Scadix.AxamlDesigner.Extensions
public static class SplitGroupGeometry
{
    public static Rect Union(IReadOnlyList<Rect> bounds);
    public static IReadOnlyList<Rect> Translate(IReadOnlyList<Rect> bounds, Vector delta);
    public static IReadOnlyList<Rect> Scale(IReadOnlyList<Rect> bounds, Rect targetUnion);
    public static IReadOnlyList<Rect> Align(IReadOnlyList<Rect> bounds, int primaryIndex, GroupAlignment alignment);
    public static IReadOnlyList<Rect> Distribute(IReadOnlyList<Rect> bounds, GroupDistribution direction);
}
```

Use normalized child coordinates within the original union for scaling. Reject non-finite values and zero-sized source axes with `ArgumentException`. Distribution sorts indices on its axis, preserves the first and last rectangles, and uses `gap = (outerSpan - sum(itemSizes)) / (count - 1)`.

- [ ] **Step 4: Run geometry and existing alignment checks**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-geometry-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --alignment-only
```

Expected: both commands exit 0 with zero failures.

- [ ] **Step 5: Commit**

```powershell
git add Library/Scadix.AxamlDesign/SplitGroupEditing.cs Library/Scadix.AxamlDesigner/Extensions/SplitGroupGeometry.cs tests/SplitViewIntegration/SplitGroupGeometryChecks.cs tests/SplitViewIntegration/Program.cs
git commit -m "Add Split group geometry calculations"
```

---

### Task 2: Atomic Multi-Item Source Persistence

**Files:**
- Modify: `Library/Scadix.AxamlDesign/Services.cs:195-315`
- Modify: `App/Scadix.Designer/Views/Documents/DocumentView.axaml.cs:47-65`
- Modify: `App/Scadix.Designer/Services/SplitPropertyEditorFactory.cs:110-305`
- Create: `tests/SplitViewIntegration/SplitGroupSourceChecks.cs`
- Modify: `tests/SplitViewIntegration/Program.cs:45-75`

**Interfaces:**
- Consumes: ordered `IReadOnlyList<DesignItem>` and matching final rectangles in common-parent coordinates.
- Produces: `CreateGroupCommit(IReadOnlyList<DesignItem>, bool)` on `ISplitResizeOverlayService`.

- [ ] **Step 1: Write failing Canvas and Grid source checks**

```csharp
var items = doc.SelectionService!.SelectedItems.ToList();
var commit = service.CreateGroupCommit(items, includeSize: false)!;
Check(commit(new[] { new Rect(18, 28, 40, 20), new Rect(66, 36, 30, 30) }), "Canvas group commit accepted");
Check(doc.Text.Contains("Canvas.Left='18'") && doc.Text.Contains("Canvas.Left='66'"), "Every Canvas child moves");
doc.UndoCommand.Execute(null);
Check(doc.Text == original, "Canvas group commit is one undo entry");
```

Add Grid fixtures covering Left/Top, Right/Bottom, Center, and Stretch margins. Add a second item with `Width="{Binding ItemWidth}"`; an `includeSize: true` commit must return false and preserve the complete source.

- [ ] **Step 2: Run source checks and verify RED**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-source-only
```

Expected: compilation fails because `CreateGroupCommit` is absent.

- [ ] **Step 3: Replace the direct-view prototype contract**

```csharp
Func<IReadOnlyList<Rect>, bool>? CreateGroupCommit(
    IReadOnlyList<DesignItem> items,
    bool includeSize);
```

Remove `AlignmentDirection`, `Direction`, `IGroupEditingService`, and `GroupEditingService` from `Services.cs`. Remove `_groupEditing` construction and registration from `DocumentView`.

- [ ] **Step 4: Implement guarded batch editing**

```csharp
public Func<IReadOnlyList<Rect>, bool>? CreateGroupCommit(
    IReadOnlyList<DesignItem> items, bool includeSize)
{
    var revision = _document.Text;
    var targets = ResolveGroupTargets(items, includeSize);
    if (targets == null) return null;
    return bounds => bounds.Count == targets.Count
        && _document.Text == revision
        && ApplyGroupEdits(targets, bounds, includeSize);
}
```

Resolve every target before returning the delegate. Reuse existing numeric formatting, `FindTarget`, `FindAttachedTarget`, expression guards, and Grid margin formulas. Build all replacements before mutation, reject invalid batches, then apply replacements in descending source-offset order within one `TextDocument.RunUpdate()`.

- [ ] **Step 5: Delegate from `DocumentView`**

```csharp
public Func<IReadOnlyList<Rect>, bool>? CreateGroupCommit(IReadOnlyList<DesignItem> items, bool includeSize)
    => Document?.IsPreviewSelectable == true
        ? new SplitPropertyEditorFactory(Document).CreateGroupCommit(items, includeSize)
        : null;
```

- [ ] **Step 6: Verify source and single-item suites**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-source-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --move-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --resize-only
```

Expected: zero failures and no partial edits for protected properties.

- [ ] **Step 7: Commit**

```powershell
git add Library/Scadix.AxamlDesign/Services.cs App/Scadix.Designer/Views/Documents/DocumentView.axaml.cs App/Scadix.Designer/Services/SplitPropertyEditorFactory.cs tests/SplitViewIntegration/SplitGroupSourceChecks.cs tests/SplitViewIntegration/Program.cs
git commit -m "Add atomic Split group source edits"
```

---

### Task 3: Group Selection Overlay and Movement

**Files:**
- Modify: `Library/Scadix.AxamlDesigner/Extensions/SplitResizeThumbExtension.cs:15-430`
- Create: `tests/SplitViewIntegration/SplitGroupInteractionChecks.cs`
- Modify: `tests/SplitViewIntegration/Program.cs:45-80`

**Interfaces:**
- Consumes: `SplitGroupGeometry.Union`, `Translate`, and `CreateGroupCommit(items, false)`.
- Produces: `SplitGroupBorderDrag` and `SplitGroupMoveHandle` overlay controls.

- [ ] **Step 1: Add failing group-selection and movement checks**

```csharp
Click(first, KeyModifiers.None);
Click(second, KeyModifiers.Control);
Check(selection.SelectionCount == 2, "Ctrl+Click selects two siblings");
Check(Handle("SplitGroupBorderDrag").IsVisible, "Eligible selection shows union border");
Drag("SplitGroupMoveHandle", new Vector(16, 8));
Check(BothCanvasPositionsChangedBy(16, 8), "Group drag moves every selected item");
```

Also check Ctrl+Click removal, primary preservation, mixed-parent and StackPanel rejection, 8 px snap, Alt bypass, arrows, Shift+Arrow, guides, readout, Escape, and one-step Undo/Redo.

- [ ] **Step 2: Run interaction checks and verify RED**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-interaction-only
```

Expected: group overlay controls are absent.

- [ ] **Step 3: Add the eligible-selection snapshot**

```csharp
private sealed record GroupSnapshot(
    IReadOnlyList<DesignItem> Items,
    IReadOnlyList<Rect> ParentBounds,
    Rect UnionBounds,
    Visual Parent);

private GroupSnapshot? TryCreateGroupSnapshot();
```

Require two or more selected controls, one identical direct parent, a Canvas or Grid parent view, finite non-empty bounds, and an available group commit. Only the primary selection's extension creates group controls.

- [ ] **Step 4: Render and move the union overlay**

```csharp
var finalBounds = SplitGroupGeometry.Translate(snapshot.ParentBounds, parentDelta);
if (commit(finalBounds)) _service.RefreshAfterKeyboardEdit();
```

During movement, snap and calculate guides from the tentative union, update only overlay geometry, and show the union readout. Capture one commit callback at gesture start. Escape clears overlay state without invoking it.

- [ ] **Step 5: Move the group from the keyboard**

```csharp
var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10d : 1d;
var finalBounds = SplitGroupGeometry.Translate(snapshot.ParentBounds, new Vector(dx * step, dy * step));
if (commit(finalBounds)) _service.RefreshAfterKeyboardEdit();
```

Arrow movement bypasses grid snapping while still showing union alignment guides.

- [ ] **Step 6: Verify interaction regressions**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-interaction-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --interaction-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --snap-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --alignment-only
```

Expected: all commands exit 0 with zero failures.

- [ ] **Step 7: Commit**

```powershell
git add Library/Scadix.AxamlDesigner/Extensions/SplitResizeThumbExtension.cs tests/SplitViewIntegration/SplitGroupInteractionChecks.cs tests/SplitViewIntegration/Program.cs
git commit -m "Add Split group movement overlay"
```

---

### Task 4: Proportional Group Resize

**Files:**
- Modify: `Library/Scadix.AxamlDesigner/Extensions/SplitResizeThumbExtension.cs`
- Modify: `tests/SplitViewIntegration/SplitGroupInteractionChecks.cs`

**Interfaces:**
- Consumes: group snapshot, `SplitGroupGeometry.Scale`, and `CreateGroupCommit(items, true)`.
- Produces: eight `SplitGroupResize{Direction}` handles.

- [ ] **Step 1: Add failing checks for all eight handles**

```csharp
foreach (var (name, delta, fixedEdge) in GroupResizeCases())
{
    await LoadFixture();
    Drag(name, delta);
    Check(OppositeEdgeStayedFixed(fixedEdge), name + " preserves the opposite edge");
    Check(ChildrenMatchProportionalTransform(), name + " scales every child");
    await AssertSingleUndoRedo();
}
```

Add Escape, active-axis snapping, Alt bypass, zero-sized-axis, minimum-size, and protected-child atomic-rejection cases.

- [ ] **Step 2: Run checks and verify RED**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-resize-only
```

Expected: group resize handles are absent.

- [ ] **Step 3: Implement tentative group resizing**

```csharp
var targetUnion = ResizeUnion(snapshot.UnionBounds, parentDelta, resizeX, resizeY);
targetUnion = SnapActiveUnionAxes(targetUnion, resizeX, resizeY, modifiers);
var finalBounds = SplitGroupGeometry.Scale(snapshot.ParentBounds, targetUnion);
```

Clamp scale factors against every child's minimum and maximum size. Recalculate left/top movement from the constrained union to keep the opposite edge fixed. Update union overlay, guides, and readout while dragging; commit once on release.

- [ ] **Step 4: Verify group and single resize suites**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-resize-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --resize-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --snap-only
```

Expected: all commands exit 0 with zero failures.

- [ ] **Step 5: Commit**

```powershell
git add Library/Scadix.AxamlDesigner/Extensions/SplitResizeThumbExtension.cs tests/SplitViewIntegration/SplitGroupInteractionChecks.cs
git commit -m "Add proportional Split group resizing"
```

---

### Task 5: Alignment and Distribution Commands

**Files:**
- Create: `Library/Scadix.AxamlDesign/Services/ISplitGroupCommandService.cs`
- Create: `App/Scadix.Designer/Services/SplitGroupCommandService.cs`
- Modify: `App/Scadix.Designer/Views/Documents/DocumentView.axaml:1-120`
- Modify: `App/Scadix.Designer/Views/Documents/DocumentView.axaml.cs:130-290`
- Create: `tests/SplitViewIntegration/SplitGroupCommandChecks.cs`
- Modify: `tests/SplitViewIntegration/Program.cs:45-85`

**Interfaces:**
- Consumes: current eligible selection, geometry helper, and group batch commit.
- Produces: `ISplitGroupCommandService` plus a Split-only command bar.

- [ ] **Step 1: Add failing command tests**

```csharp
Check(service.CanAlign && service.CanDistribute, "Three siblings enable commands");
Check(service.Align(GroupAlignment.Right), "Right alignment commits");
Check(AllRightEdgesEqualPrimary(), "Alignment uses the primary item");
doc.UndoCommand.Execute(null);
Check(service.Distribute(GroupDistribution.Horizontal), "Horizontal distribution commits");
Check(EqualHorizontalGaps() && OuterItemsUnchanged(), "Distribution preserves outer items");
```

Cover six alignment values, two distributions, eligibility counts, mixed parents, protected properties, and one-step Undo/Redo.

- [ ] **Step 2: Run checks and verify RED**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-command-only
```

Expected: compilation fails because the command service is absent.

- [ ] **Step 3: Define and implement the command service**

```csharp
public interface ISplitGroupCommandService
{
    bool CanAlign { get; }
    bool CanDistribute { get; }
    bool Align(GroupAlignment alignment);
    bool Distribute(GroupDistribution direction);
}
```

The service obtains a fresh eligible snapshot, calculates final rectangles with `SplitGroupGeometry`, commits once, and requests one preview refresh. It stores no gesture state.

- [ ] **Step 4: Add the Split-only command bar**

```xml
<StackPanel x:Name="SplitGroupCommandBar" Orientation="Horizontal" IsVisible="False">
  <Button ToolTip.Tip="Align left" Click="AlignLeft_Click" Content="L"/>
  <Button ToolTip.Tip="Distribute horizontally" Click="DistributeHorizontal_Click" Content="H↔"/>
</StackPanel>
```

Add buttons for Left, H-Center, Right, Top, V-Center, Bottom, Distribute Horizontal, and Distribute Vertical. Refresh visibility/enabled state on selection change, preview reload, and mode change.

- [ ] **Step 5: Register and dispose the service**

```csharp
_groupCommands = new SplitGroupCommandService(
    Document.SelectionService,
    CreateEligibleGroupSnapshot,
    RefreshAfterKeyboardEdit);
context.Services.AddOrReplaceService(typeof(ISplitGroupCommandService), _groupCommands);
```

Construct only after selection and context exist. Replace after preview reload and unsubscribe on document detach; never pass a nullable selection service.

- [ ] **Step 6: Verify commands and lifecycle**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-command-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --alignment-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet
```

Expected: all commands exit 0 with zero failures.

- [ ] **Step 7: Commit**

```powershell
git add Library/Scadix.AxamlDesign/Services/ISplitGroupCommandService.cs App/Scadix.Designer/Services/SplitGroupCommandService.cs App/Scadix.Designer/Views/Documents/DocumentView.axaml App/Scadix.Designer/Views/Documents/DocumentView.axaml.cs tests/SplitViewIntegration/SplitGroupCommandChecks.cs tests/SplitViewIntegration/Program.cs
git commit -m "Add Split group alignment commands"
```

---

### Task 6: Lifecycle and Final Regression Gate

**Files:**
- Modify: `tests/SplitViewIntegration/SplitGroupInteractionChecks.cs`
- Modify: `tests/SplitViewIntegration/SplitGroupSourceChecks.cs`
- Modify: `tests/SplitViewIntegration/SplitGroupCommandChecks.cs`
- Modify only when a reproduced failure requires it: production files from Tasks 1-5.

**Interfaces:**
- Consumes: completed group overlay, command service, and batch persistence.
- Produces: release-ready regression coverage and rebuilt app.

- [ ] **Step 1: Add lifecycle and exact-format checks**

```csharp
Check(SourceExceptExpectedAttributesIsByteExact(), "Group edit preserves unrelated XAML");
BeginGroupDrag(); TypeInSourceEditor();
Check(NoGroupHandlesRemain() && doc.Text == typedSource, "Typing cancels a stale gesture");
BeginGroupDrag(); SwitchToDesignMode();
Check(NoGroupHandlesRemain(), "Mode change clears group overlays");
```

Repeat cancellation checks for preview reload and document detach. Verify selected items and the primary item reconnect after a successful batch refresh.

- [ ] **Step 2: Run focused lifecycle suites**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-interaction-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-source-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --group-command-only
```

Expected: zero failures. Trace each failure to the state owner and add cleanup at its existing detach, reload, or mode-change boundary.

- [ ] **Step 3: Run all focused Split suites**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --move-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --resize-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --interaction-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --snap-only
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet -- --alignment-only
```

Expected: every command exits 0 with zero failures.

- [ ] **Step 4: Run the full integration and build gate**

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -v quiet
dotnet build App/Scadix.Designer/Scadix.Designer.csproj -v quiet
git diff --check
```

Expected: full suite reports zero failures; build reports zero errors; diff check reports none. The existing `NU1903` warning for `Tmds.DBus.Protocol` 0.21.2 may remain.

- [ ] **Step 5: Manually smoke-test the rebuilt executable**

```powershell
& 'D:\Research\ScadixFree\App\Scadix.Designer\bin\Debug\net10.0-windows\Scadix.Designer.exe'
```

Open a Canvas/Grid fixture, enter Split mode, Ctrl+Click two or three siblings, and verify hit targets, cursors, union overlay, movement, eight resize handles, command bar, guides, readout, Undo/Redo, and Escape.

- [ ] **Step 6: Commit any lifecycle fixes**

```powershell
git add tests/SplitViewIntegration/SplitGroupInteractionChecks.cs tests/SplitViewIntegration/SplitGroupSourceChecks.cs tests/SplitViewIntegration/SplitGroupCommandChecks.cs Library/Scadix.AxamlDesigner/Extensions/SplitResizeThumbExtension.cs App/Scadix.Designer/Views/Documents/DocumentView.axaml.cs App/Scadix.Designer/Services/SplitPropertyEditorFactory.cs
git commit -m "Harden Split group editing lifecycle"
```

If no files changed during lifecycle verification, skip this commit.
