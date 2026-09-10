# Split view integration checks

Two-way selection checks cover source-caret navigation into root/nested controls and closing tags, caret preservation, Properties/Outline synchronization, clicking an already selected control, and restoring selection after source edits reload the preview.

Requires .NET 10 and an interactive Windows desktop. From the repository root:

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj
```

The harness briefly opens a real document view and Properties window. It verifies preview clicks on nested and templated controls, matching XAML tag selection (including multiline source and quoted angle brackets), Outline synchronization, stale-selection clearing, selection after preview reload, debounce, exact saves, invalid-XAML recovery, mode transitions, retained column proportions, detach/reattach, and editor-only handling of non-XAML files.

Properties checks exercise actual text editors: Enter/lost-focus commit, Escape cancellation, source-history Undo/Redo and Ctrl+Z/Ctrl+Y, retained selection, XML escaping, empty attributes, single quotes, CRLF/comments, attribute insertion, automatic width, spacing and color changes. Invalid numbers/XML characters, stale editors, bindings, resources and complex property elements must leave the source intact. Rename and advanced mutation menus stay disabled in Split.

In Split, the supported fields are Text, Content, Width/Height (including Auto), MinWidth/MinHeight, MaxWidth/MaxHeight, Margin, Padding, BorderThickness, Background, Foreground, BorderBrush, FontSize and Opacity. Colors accept named colors or hex values. Other properties remain available for inspection; edit their XAML directly.

Split resize provides eight handles on the selected control. Drag a corner or edge, then release to write Width/Height as one source undo step. Ctrl on a corner preserves aspect ratio; Escape or losing capture cancels. The handles show the tentative dimensions while dragging; the preview reloads after release. Binding/resource and complex dimensions remain protected. Left/top resize now also adjusts position to keep the opposite edge fixed; the interaction checklist checks both feedback and final layout.

Resize checks cover real hit testing, pointer events for all eight handles, Ctrl aspect ratio, Escape, no-op clicks, Undo/Redo, transformed coordinates, minimum width, absent attributes, exact saves, protected bindings, and cleanup after source edits and mode changes. Run just these checks with:

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -- --resize-only
```

Run the move checklist separately with:

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -- --move-only
```

Move checks cover center/body hit testing, Canvas coordinates, Grid margins and actual displacement, Escape routed through the focused element, single-step Undo/Redo commands, nested panels, StackPanel/WrapPanel exclusions, binding/DynamicResource preservation, exact source formatting, culture-sensitive coordinates, and transformed drag feedback. They collect assertion failures before exiting. Pointer handlers are also exercised directly, so passing source-edit checks alone does not prove that a handle is reachable by mouse. Move Undo/Redo tests invoke document commands; native Ctrl+Z/Ctrl+Y input is not simulated.

Move handles sit above the drag body; Escape cancels both drag targets. Coordinates are converted to the parent's space on release, while overlay feedback follows the preview transform. Canvas coordinates absent from source start from the arranged position, including opposite anchors. Attached property elements remain protected. Grid controls aligned Right or Bottom now offer move handles and change the corresponding trailing margins. Tests also check centered movement against actual arranged displacement; missing local Margin starts from the effective styled value.

Run the expanded interaction checklist separately:

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj -- --interaction-only
```

This checks Canvas left/top resize feedback and fixed opposite edges (including Ctrl), Grid Right/Bottom and Center movement, all four arrow keys with and without Shift after clicking/releasing a move handle, operation-level Undo/Redo, and Escape cancellation including an arrow pressed during a drag. Keyboard events are routed through the actual focused element. These are synthetic Avalonia events in a real window, not native OS mouse/keyboard automation. Assertion failures are collected before a nonzero exit.

Additional checks cover keyboard focus after preview selection and drag/reload, separate undo for repeated arrows, protected Canvas position bindings, and Grid resize with leading, centered and trailing alignment. Append `--extra-only` to run these additional cases alone. Arrow keys move by one parent-coordinate unit (Shift: ten) when the preview overlay has focus. During pointer dragging they are ignored so Escape can cancel the whole operation. Resize accounts for the position shift caused by Grid alignment as dimensions change; Canvas resize rejects an operation that requires changing a protected coordinate.

The process exits nonzero on failure. A screenshot is saved to `artifacts/SplitView/split.png`. Selection, move and resize tests raise Avalonia pointer events on the actual controls. The column ratio check changes grid widths directly; native OS pointer dragging is not automated.
