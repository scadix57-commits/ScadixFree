using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.Designer;

internal static class SplitGroupSourceChecks
{
    public static async Task Run(Document doc, DocumentView view)
    {
        var editor = view.GetVisualDescendants().OfType<XamlEditorView>().Single().Editor!;
        var service = doc.DesignContext!.Services.GetService<ISplitResizeOverlayService>()!;

        async Task<IReadOnlyList<DesignItem>> Load(string source)
        {
            editor.Text = source;
            await Task.Delay(750);
            var items = new List<DesignItem>();
            var offset = 0;
            while ((offset = source.IndexOf("<Button", offset, StringComparison.Ordinal)) >= 0)
            {
                editor.Select(offset + 2, 0);
                await Task.Delay(100);
                items.Add(doc.SelectionService!.PrimarySelection);
                offset += "<Button".Length;
            }
            doc.SelectionService!.SetSelectedComponents(items);
            return doc.SelectionService.SelectedItems.OrderBy(item => ((Button)item.Component).Content?.ToString()).ToList();
        }

        const string canvas = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\"><Canvas><Button Content='A' Width='40' Height='20' Canvas.Left='10' Canvas.Top='20' /><Button Content='B' Width='30' Height='30' Canvas.Left='50' Canvas.Top='30' /></Canvas></UserControl>";
        var items = await Load(canvas);
        var original = doc.Text;
        var commit = service.CreateGroupCommit(items, includeSize: false)!;
        Check(commit(new[] { new Rect(18, 28, 40, 20), new Rect(66, 36, 30, 30) }), "Canvas group commit accepted");
        Check(doc.Text.Contains("Canvas.Left='18'") && doc.Text.Contains("Canvas.Left='66'") &&
              doc.Text.Contains("Canvas.Top='28'") && doc.Text.Contains("Canvas.Top='36'"), "Every Canvas child moves");
        var changed = doc.Text;
        Check(changed == original.Replace("Canvas.Left='10'", "Canvas.Left='18'")
            .Replace("Canvas.Top='20'", "Canvas.Top='28'")
            .Replace("Canvas.Left='50'", "Canvas.Left='66'")
            .Replace("Canvas.Top='30'", "Canvas.Top='36'"),
            "Group commit changes only the expected attribute values");
        doc.UndoCommand.Execute(null);
        Check(doc.Text == original, "Canvas group commit is one undo entry");
        await Task.Delay(750);
        doc.RedoCommand.Execute(null);
        Check(doc.Text == changed, "Canvas group commit supports one-step Redo");
        await Task.Delay(750);

        const string canvasMargins = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\"><Canvas><Button Content='A' Width='40' Height='20' Margin='3,4,0,0' Canvas.Left='10' Canvas.Top='20' /><Button Content='B' Width='30' Height='30' Margin='5,6,0,0' Canvas.Left='50' Canvas.Top='30' /></Canvas></UserControl>";
        items = await Load(canvasMargins);
        commit = service.CreateGroupCommit(items, includeSize: false)!;
        Check(commit(new[] { new Rect(21, 32, 40, 20), new Rect(75, 46, 30, 30) }), "Canvas margin group move accepted");
        Check(doc.Text.Contains("Canvas.Left='18' Canvas.Top='28'") && doc.Text.Contains("Canvas.Left='70' Canvas.Top='40'"), "Canvas group move subtracts leading margins from final bounds");

        items = await Load(canvasMargins);
        commit = service.CreateGroupCommit(items, includeSize: true)!;
        Check(commit(new[] { new Rect(28, 35, 50, 25), new Rect(80, 55, 35, 35) }), "Canvas margin group resize accepted");
        Check(doc.Text.Contains("Width='50' Height='25' Margin='3,4,0,0' Canvas.Left='25' Canvas.Top='31'") &&
              doc.Text.Contains("Width='35' Height='35' Margin='5,6,0,0' Canvas.Left='75' Canvas.Top='49'"), "Canvas group resize preserves final positions with margins");

        const string missingCanvasProperties = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\"><Canvas><Button Content='A' /></Canvas></UserControl>";
        items = await Load(missingCanvasProperties);
        commit = service.CreateGroupCommit(items, includeSize: true)!;
        Check(commit(new[] { new Rect(18, 28, 40, 20) }), "Canvas group commit inserts missing properties");
        Check(doc.Text.Contains("Canvas.Left=\"18\"") && doc.Text.Contains("Canvas.Top=\"28\"") &&
              doc.Text.Contains("Width=\"40\"") && doc.Text.Contains("Height=\"20\""), "Missing Canvas properties are inserted together");

        const string leftTop = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\"><Grid><Button Content='A' Width='40' Height='20' Margin='10,20,3,4' HorizontalAlignment='Left' VerticalAlignment='Top' /></Grid></UserControl>";
        items = await Load(leftTop);
        commit = service.CreateGroupCommit(items, includeSize: true)!;
        Check(commit(new[] { new Rect(18, 28, 50, 25) }), "Grid Left/Top group commit accepted");
        Check(doc.Text.Contains("Width='50' Height='25' Margin='18,28,3,4'"), "Grid Left/Top updates leading margins");

        const string rightBottom = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\"><Grid><Button Content='A' Width='40' Height='20' Margin='3,4,10,20' HorizontalAlignment='Right' VerticalAlignment='Bottom' /></Grid></UserControl>";
        items = await Load(rightBottom);
        var rightBottomButton = (Button)items.Single().Component;
        commit = service.CreateGroupCommit(items, includeSize: true)!;
        Check(commit(new[] { new Rect(rightBottomButton.Bounds.X - 8, rightBottomButton.Bounds.Y - 8, 50, 25) }), "Grid Right/Bottom group commit accepted");
        Check(doc.Text.Contains("Width='50' Height='25' Margin='3,4,8,23'"), "Grid Right/Bottom preserves trailing anchors");

        const string center = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\"><Grid><Button Content='A' Width='40' Height='20' Margin='3,4,5,6' HorizontalAlignment='Center' VerticalAlignment='Center' /></Grid></UserControl>";
        items = await Load(center);
        var centerButton = (Button)items.Single().Component;
        commit = service.CreateGroupCommit(items, includeSize: true)!;
        Check(commit(new[] { new Rect(centerButton.Bounds.X + 8, centerButton.Bounds.Y + 5, 50, 25) }), "Grid Center group commit accepted");
        Check(doc.Text.Contains("Width='50' Height='25' Margin='29,19,5,6'"), "Grid Center applies doubled offsets and size compensation");

        const string stretch = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\"><Grid><Button Content='A' Width='40' Height='20' Margin='3,4,5,6' HorizontalAlignment='Stretch' VerticalAlignment='Stretch' /></Grid></UserControl>";
        items = await Load(stretch);
        var stretchButton = (Button)items.Single().Component;
        commit = service.CreateGroupCommit(items, includeSize: true)!;
        Check(commit(new[] { new Rect(stretchButton.Bounds.X + 8, stretchButton.Bounds.Y + 5, 50, 25) }), "Grid Stretch group commit accepted");
        Check(doc.Text.Contains("Width='50' Height='25' Margin='11,9,5,6'"), "Grid Stretch updates leading margins only");

        const string protectedSize = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\"><Canvas><Button Content='A' Width='{Binding ItemWidth}' Height='20' Canvas.Left='10' Canvas.Top='20' /><Button Content='B' Width='30' Height='30' Canvas.Left='50' Canvas.Top='30' /></Canvas></UserControl>";
        items = await Load(protectedSize);
        original = doc.Text;
        Check(service.CreateGroupCommit(items, includeSize: true) == null, "Bound group dimension prevents commit creation");
        Check(doc.Text == original, "Protected group source remains exact");

        const string protectedLater = "<UserControl xmlns=\"https://github.com/avaloniaui\" Width=\"320\" Height=\"240\"><Canvas><Button Content='A' Width='40' Height='20' Canvas.Left='10' Canvas.Top='20' /><Button Content='B' Width='30' Height='30' Canvas.Left='50' Canvas.Top='{Binding ItemTop}' /></Canvas></UserControl>";
        items = await Load(protectedLater);
        original = doc.Text;
        Check(service.CreateGroupCommit(items, includeSize: false) == null,
            "Protected later group item prevents an atomic position commit");
        Check(doc.Text == original, "Protected later item leaves every source byte intact");

        items = await Load(canvas);
        commit = service.CreateGroupCommit(items, includeSize: false)!;
        var stale = doc.Text + "\r\n<!-- external source change -->";
        editor.Text = stale;
        Check(!commit(new[] { new Rect(18, 28, 40, 20), new Rect(66, 36, 30, 30) }) && doc.Text == stale,
            "Stale group commit rejects atomically without overwriting newer source");
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}
