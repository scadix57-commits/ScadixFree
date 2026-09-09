# Split view integration checks

Two-way selection checks cover source-caret navigation into root/nested controls and closing tags, caret preservation, Properties/Outline synchronization, clicking an already selected control, and restoring selection after source edits reload the preview.

Requires .NET 10 and an interactive Windows desktop. From the repository root:

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj
```

The harness briefly opens a real document view and Properties window. It verifies preview clicks on nested and templated controls, matching XAML tag selection (including multiline source and quoted angle brackets), Outline synchronization, stale-selection clearing, selection after preview reload, debounce, exact saves, invalid-XAML recovery, mode transitions, retained column proportions, detach/reattach, and editor-only handling of non-XAML files.

Properties checks exercise actual text editors: Enter/lost-focus commit, Escape cancellation, source-history Undo/Redo and Ctrl+Z/Ctrl+Y, retained selection, XML escaping, empty attributes, single quotes, CRLF/comments, attribute insertion, automatic width, spacing and color changes. Invalid numbers/XML characters, stale editors, bindings, resources and complex property elements must leave the source intact. Rename and advanced mutation menus stay disabled in Split.

In Split, the supported fields are Text, Content, Width/Height (including Auto), MinWidth/MinHeight, MaxWidth/MaxHeight, Margin, Padding, BorderThickness, Background, Foreground, BorderBrush, FontSize and Opacity. Colors accept named colors or hex values. Other properties remain available for inspection; edit their XAML directly.

The process exits nonzero on failure. A screenshot is saved to `artifacts/SplitView/split.png`. Selection tests raise Avalonia pointer events on the actual overlay. The ratio check changes grid widths directly; native OS pointer dragging is not automated.
