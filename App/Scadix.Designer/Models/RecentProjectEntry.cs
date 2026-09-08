using System;
using System.IO;

namespace Scadix.Designer;

/// <summary>
/// Represents a recently opened project/solution/folder in the WelcomeScreen list.
/// </summary>
public class RecentProjectEntry
{
    public string Name     { get; set; } = "";
    public string Path     { get; set; } = "";
    public string LastOpened { get; set; } = "Today";
    public bool   IsFolder { get; set; }

    /// <summary>First letter of the name — used as the icon letter.</summary>
    public string IconLetter => string.IsNullOrEmpty(Name) ? "?" : Name[0].ToString().ToUpperInvariant();

    /// <summary>Accent color for the icon background — cycles through a palette.</summary>
    public string IconColor { get; set; } = "#4D78CC";

    // ── Palette ──────────────────────────────────────────────────────────
    private static readonly string[] _palette =
    {
        "#C21460", "#2E7D32", "#1565C0", "#6A1B9A",
        "#E65100", "#00695C", "#4527A0", "#AD1457",
        "#0277BD", "#558B2F"
    };

    private static int _colorIndex;

    public static RecentProjectEntry FromPath(string path)
    {
        var isFolder = Directory.Exists(path) &&
                       !path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) &&
                       !path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) &&
                       !path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

        var name = isFolder
            ? System.IO.Path.GetFileName(path.TrimEnd('/', '\\'))
            : System.IO.Path.GetFileNameWithoutExtension(path);

        var color = _palette[_colorIndex % _palette.Length];
        _colorIndex++;

        return new RecentProjectEntry
        {
            Name        = name,
            Path        = path,
            LastOpened  = "Today",
            IsFolder    = isFolder,
            IconColor   = color
        };
    }
}
