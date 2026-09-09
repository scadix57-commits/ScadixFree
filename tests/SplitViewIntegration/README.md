# Split view integration checks

Two-way selection checks cover source-caret navigation into root/nested controls and closing tags, caret preservation, Properties/Outline synchronization, clicking an already selected control, and restoring selection after source edits reload the preview.

Requires .NET 10 and an interactive Windows desktop. From the repository root:

```powershell
dotnet run --project tests/SplitViewIntegration/SplitViewIntegration.csproj
```

The harness briefly opens a real document view and Properties window. It verifies preview clicks on nested and templated controls, matching XAML tag selection (including multiline source and quoted angle brackets), Outline synchronization, read-only property editors with inspection navigation enabled, stale-selection clearing, selection after preview reload, debounce, exact saves, invalid-XAML recovery, mode transitions, retained column proportions, detach/reattach, and editor-only handling of non-XAML files.

The process exits nonzero on failure. A screenshot is saved to `artifacts/SplitView/split.png`. Selection tests raise Avalonia pointer events on the actual overlay. The ratio check changes grid widths directly; native OS pointer dragging is not automated.
