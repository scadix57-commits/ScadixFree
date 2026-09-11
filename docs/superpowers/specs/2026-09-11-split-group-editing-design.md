# Split View Group Editing Design

## Goal

Add source-preserving multi-selection editing to Split mode. A user can Ctrl+Click controls that share one direct Canvas or Grid parent, then move, proportionally resize, align, or distribute them while the preview, XAML source, selection, and undo history remain synchronized.

## Scope

The first release supports sibling controls with the same direct Canvas or Grid parent. A mixed-parent selection remains selectable, but group editing controls are hidden and commands are disabled. StackPanel, WrapPanel, and other layout-managed parents keep their existing selection and single-control resize behavior.

Group editing includes:

- Ctrl+Click selection toggling.
- A union-bounds border, center move handle, and eight resize handles.
- Group move with 8 px snapping, alignment guides, and Alt to bypass snapping.
- Proportional group resize around the opposite union edge or corner.
- Align left, horizontal center, right, top, vertical center, and bottom.
- Distribute equal horizontal or vertical gaps.
- Arrow movement by 1 px and Shift+Arrow movement by 10 px.
- One undo entry per completed gesture or command.
- Escape to cancel an active move or resize.

Rotating controls, cross-parent transformations, percentage-based Grid layout, and persisting named groups are outside this scope.

## Interaction Model

The primary selection anchors keyboard focus and determines which overlay owns the group adorners. Ctrl+Click toggles secondary items. Selecting the root, an ancestor, or an item with a different direct parent replaces the current selection rather than creating an editable mixed-parent group.

The union rectangle is calculated in the common parent's coordinate space. Dragging its border or center handle translates every item by the same vector. Dragging a resize handle calculates a new union rectangle; each child rectangle is transformed from its original normalized position and size within the original union rectangle. The opposite edge or corner remains fixed. Minimum sizes are applied per child, and an invalid scale is rejected without changing source.

Snapping and alignment guides operate on the tentative union rectangle. Holding Alt disables both. The size/position readout describes the union rectangle during a gesture and disappears on completion or cancellation.

Alignment uses the primary item as the reference. Distribution preserves the outermost items and assigns equal gaps between adjacent bounds after sorting on the requested axis. Commands require at least two items for alignment and at least three for distribution.

## Architecture

`SplitResizeThumbExtension` remains the interaction owner. It detects an eligible multi-selection and switches from single-item adorners to group adorners. Pointer and keyboard events produce tentative rectangles only; they do not mutate runtime controls directly.

`ISplitResizeOverlayService` gains group commit factories that accept all affected `DesignItem` instances and their final rectangles. `DocumentView` implements those factories by translating the rectangles into a single batch of source edits. Existing single-item commit factories remain unchanged.

Alignment and distribution are exposed through a small group-command service registered in the document's `DesignContext`. The service computes final rectangles and delegates persistence to the same batch source-edit path used by gestures. It contains no Avalonia view mutation and no stored cancellation state.

The prototype `GroupEditingService` currently placed in `Services.cs` will be replaced. Its direct writes to `DesignItem.Position` and `View.Width`/`View.Height` cannot provide source preservation or transactional Undo/Redo.

## Source Updates

Each operation captures the current editor revision and resolves every selected item to its opening-tag source span before editing. If any item cannot be resolved, has changed since the gesture began, or contains a protected binding/resource expression in a property that must change, the whole operation is rejected.

For Canvas children, persistence updates or inserts `Canvas.Left`, `Canvas.Top`, `Width`, and `Height` as needed. For Grid children, it updates `Margin` according to horizontal and vertical alignment and writes explicit dimensions only for resize operations. Existing quote style, attribute spacing, indentation, comments, and unrelated attributes are preserved.

Edits are applied from the end of the document toward the beginning so earlier source spans remain valid. The complete batch is pushed as one editor undo operation, followed by one preview refresh that restores the selected items and primary selection.

## Transaction and Cancellation

At gesture start, the extension snapshots selection identity, source revision, original union bounds, and every original child rectangle. Pointer movement updates only overlay geometry and guides. Pointer release commits one source batch. Escape discards the tentative overlay and refreshes it from the unchanged preview; therefore cancellation needs no reverse mutation.

Alignment and distribution commands calculate and commit once, creating one Undo/Redo entry. If validation fails for any item, no partial edit is written.

## Error Handling

Unsupported selections hide group handles and disable group commands. Protected or unresolved source properties leave source unchanged and show no stale preview state. Zero-width or zero-height union bounds disable resizing on the affected axis. Preview reload, document close, mode change, or source typing cancels any active gesture and clears group overlays and guides.

## Testing

Integration tests will verify:

- Ctrl+Click adds and removes siblings while preserving the primary item.
- Mixed-parent and unsupported-parent selections do not expose group editing.
- Canvas and Grid groups move together and update the correct XAML properties.
- All eight group handles preserve the opposite union edge or corner and proportionally transform every child.
- Snapping, Alt bypass, alignment guides, readout, arrows, and Shift+Arrow use union bounds.
- Alignment uses the primary item for all six directions.
- Horizontal and vertical distribution preserve outer items and create equal gaps.
- One Undo and Redo covers every gesture or command.
- Escape, mode changes, reloads, and source typing cancel active gestures.
- Bindings and resource references cause atomic rejection.
- Formatting and unrelated XAML remain byte-for-byte unchanged.
- Existing single-selection move, resize, snapping, guide, property, and mode tests remain green.

Manual smoke testing will confirm hit targets, cursor feedback, overlay visibility, and interaction quality in `Scadix.Designer.exe`.
