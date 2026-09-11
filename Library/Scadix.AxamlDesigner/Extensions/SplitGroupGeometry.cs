using Avalonia;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.Extensions;

public static class SplitGroupGeometry
{
    public static Rect Union(IReadOnlyList<Rect> bounds)
    {
        ValidateBounds(bounds);
        if (bounds.Count == 0)
            throw new ArgumentException("At least one rectangle is required.", nameof(bounds));

        var union = bounds[0];
        for (var i = 1; i < bounds.Count; i++)
            union = union.Union(bounds[i]);
        ValidateRect(union, nameof(bounds));
        return union;
    }

    public static IReadOnlyList<Rect> Translate(IReadOnlyList<Rect> bounds, Vector delta)
    {
        ValidateBounds(bounds);
        ValidateFinite(delta.X, nameof(delta));
        ValidateFinite(delta.Y, nameof(delta));

        var translated = new Rect[bounds.Count];
        for (var i = 0; i < bounds.Count; i++)
        {
            var bound = bounds[i];
            translated[i] = new Rect(bound.X + delta.X, bound.Y + delta.Y, bound.Width, bound.Height);
            ValidateRect(translated[i], nameof(bounds));
        }
        return translated;
    }

    public static IReadOnlyList<Rect> Scale(IReadOnlyList<Rect> bounds, Rect targetUnion)
    {
        ValidateBounds(bounds);
        ValidateRect(targetUnion, nameof(targetUnion));
        var sourceUnion = Union(bounds);
        if (sourceUnion.Width == 0 || sourceUnion.Height == 0)
            throw new ArgumentException("The source union must have non-zero width and height.", nameof(bounds));

        var scaled = new Rect[bounds.Count];
        for (var i = 0; i < bounds.Count; i++)
        {
            var bound = bounds[i];
            var normalizedX = (bound.X - sourceUnion.X) / sourceUnion.Width;
            var normalizedY = (bound.Y - sourceUnion.Y) / sourceUnion.Height;
            var normalizedWidth = bound.Width / sourceUnion.Width;
            var normalizedHeight = bound.Height / sourceUnion.Height;
            scaled[i] = new Rect(
                targetUnion.X + normalizedX * targetUnion.Width,
                targetUnion.Y + normalizedY * targetUnion.Height,
                normalizedWidth * targetUnion.Width,
                normalizedHeight * targetUnion.Height);
            ValidateRect(scaled[i], nameof(bounds));
        }
        return scaled;
    }

    public static IReadOnlyList<Rect> Align(IReadOnlyList<Rect> bounds, int primaryIndex, GroupAlignment alignment)
    {
        ValidateBounds(bounds);
        if ((uint)primaryIndex >= (uint)bounds.Count)
            throw new ArgumentOutOfRangeException(nameof(primaryIndex));

        var primary = bounds[primaryIndex];
        var result = bounds.ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            var bound = result[i];
            result[i] = alignment switch
            {
                GroupAlignment.Left => new Rect(primary.Left, bound.Y, bound.Width, bound.Height),
                GroupAlignment.HorizontalCenter => new Rect(primary.Center.X - bound.Width / 2, bound.Y, bound.Width, bound.Height),
                GroupAlignment.Right => new Rect(primary.Right - bound.Width, bound.Y, bound.Width, bound.Height),
                GroupAlignment.Top => new Rect(bound.X, primary.Top, bound.Width, bound.Height),
                GroupAlignment.VerticalCenter => new Rect(bound.X, primary.Center.Y - bound.Height / 2, bound.Width, bound.Height),
                GroupAlignment.Bottom => new Rect(bound.X, primary.Bottom - bound.Height, bound.Width, bound.Height),
                _ => throw new ArgumentOutOfRangeException(nameof(alignment))
            };
            ValidateRect(result[i], nameof(bounds));
        }
        return result;
    }

    public static IReadOnlyList<Rect> Distribute(IReadOnlyList<Rect> bounds, GroupDistribution direction)
    {
        ValidateBounds(bounds);
        if (direction is not (GroupDistribution.Horizontal or GroupDistribution.Vertical))
            throw new ArgumentOutOfRangeException(nameof(direction));

        var result = bounds.ToArray();
        if (result.Length < 3)
            return result;

        var indices = Enumerable.Range(0, result.Length)
            .OrderBy(index => direction == GroupDistribution.Horizontal ? result[index].X : result[index].Y)
            .ThenBy(index => index)
            .ToArray();
        var first = result[indices[0]];
        var last = result[indices[^1]];
        var outerSpan = direction == GroupDistribution.Horizontal
            ? last.Right - first.Left
            : last.Bottom - first.Top;
        var itemSizes = result.Sum(bound => direction == GroupDistribution.Horizontal ? bound.Width : bound.Height);
        var gap = (outerSpan - itemSizes) / (result.Length - 1);
        ValidateFinite(gap, nameof(bounds));

        var position = direction == GroupDistribution.Horizontal ? first.Right : first.Bottom;
        for (var sorted = 1; sorted < indices.Length - 1; sorted++)
        {
            var index = indices[sorted];
            var bound = result[index];
            position += gap;
            result[index] = direction == GroupDistribution.Horizontal
                ? new Rect(position, bound.Y, bound.Width, bound.Height)
                : new Rect(bound.X, position, bound.Width, bound.Height);
            ValidateRect(result[index], nameof(bounds));
            position += direction == GroupDistribution.Horizontal ? bound.Width : bound.Height;
        }
        return result;
    }

    private static void ValidateBounds(IReadOnlyList<Rect> bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        for (var i = 0; i < bounds.Count; i++)
            ValidateRect(bounds[i], nameof(bounds));
    }

    private static void ValidateRect(Rect rect, string parameterName)
    {
        ValidateFinite(rect.X, parameterName);
        ValidateFinite(rect.Y, parameterName);
        ValidateFinite(rect.Width, parameterName);
        ValidateFinite(rect.Height, parameterName);
        ValidateFinite(rect.Right, parameterName);
        ValidateFinite(rect.Bottom, parameterName);
        if (rect.Width < 0 || rect.Height < 0)
            throw new ArgumentException("Rectangle dimensions cannot be negative.", parameterName);
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
            throw new ArgumentException("Geometry values must be finite.", parameterName);
    }
}
