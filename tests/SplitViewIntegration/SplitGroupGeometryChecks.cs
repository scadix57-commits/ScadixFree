using Avalonia;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Extensions;

internal static class SplitGroupGeometryChecks
{
    public static int Run()
    {
        try
        {
            var items = new[] { new Rect(10, 20, 20, 10), new Rect(50, 40, 10, 20) };
            Check(SplitGroupGeometry.Union(items) == new Rect(10, 20, 50, 40), "Union encloses every item");
            Check(SplitGroupGeometry.Translate(items, new Vector(8, -2))[1] == new Rect(58, 38, 10, 20), "Translate preserves relative geometry");

            var scaled = SplitGroupGeometry.Scale(items, new Rect(10, 20, 100, 80));
            Check(scaled[0] == new Rect(10, 20, 40, 20) && scaled[1] == new Rect(90, 60, 20, 40), "Scale is proportional");
            Check(SplitGroupGeometry.Align(items, 1, GroupAlignment.Left)[0].X == 50, "Align uses primary item");

            var horizontal = new[]
            {
                new Rect(42, 8, 6, 5),
                new Rect(80, 2, 8, 7),
                new Rect(10, 5, 10, 4)
            };
            var horizontalDistributed = SplitGroupGeometry.Distribute(horizontal, GroupDistribution.Horizontal);
            Check(horizontalDistributed[1] == horizontal[1] && horizontalDistributed[2] == horizontal[2],
                "Horizontal distribution preserves outer rectangles");
            Check(horizontalDistributed[0].X == 47 && horizontalDistributed[0].Width == horizontal[0].Width,
                "Horizontal distribution uses equal gaps");
            Check(horizontalDistributed[0].X - horizontalDistributed[2].Right == 27
                && horizontalDistributed[1].X - horizontalDistributed[0].Right == 27,
                "Horizontal distribution gaps are equal");

            var vertical = new[]
            {
                new Rect(8, 42, 5, 6),
                new Rect(2, 80, 7, 8),
                new Rect(5, 10, 4, 10)
            };
            var verticalDistributed = SplitGroupGeometry.Distribute(vertical, GroupDistribution.Vertical);
            Check(verticalDistributed[1] == vertical[1] && verticalDistributed[2] == vertical[2],
                "Vertical distribution preserves outer rectangles");
            Check(verticalDistributed[0].Y == 47 && verticalDistributed[0].Height == vertical[0].Height,
                "Vertical distribution uses equal gaps");
            Check(verticalDistributed[0].Y - verticalDistributed[2].Bottom == 27
                && verticalDistributed[1].Y - verticalDistributed[0].Bottom == 27,
                "Vertical distribution gaps are equal");

            Console.WriteLine("TOTAL GROUP GEOMETRY FAILURES: 0");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 1;
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok)
            throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}
