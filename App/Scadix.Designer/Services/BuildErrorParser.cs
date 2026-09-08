using System;
using System.Text.RegularExpressions;

namespace Scadix.Designer.Services;

/// <summary>
/// Parses raw dotnet build/restore output lines and extracts structured error/warning entries.
/// Supports MSBuild diagnostic format:
///   FilePath(Line,Col): error|warning CSXXXX: Message [ProjectFile]
/// </summary>
public static class BuildErrorParser
{
    // MSBuild format: path(line,col): error CS0001: message [project.csproj]
    private static readonly Regex _msbuildDiag = new Regex(
        @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\)\s*:\s*(?<severity>error|warning|info)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<msg>.+?)(?:\s*\[(?<proj>[^\]]+)\])?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // MSBuild format without column: path(line): error CS0001: message [project]
    private static readonly Regex _msbuildNocol = new Regex(
        @"^(?<file>.+?)\((?<line>\d+)\)\s*:\s*(?<severity>error|warning|info)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<msg>.+?)(?:\s*\[(?<proj>[^\]]+)\])?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // MSBuild format without location: error CS0001: message [project]
    private static readonly Regex _msbuildNoLoc = new Regex(
        @"^(?<severity>error|warning|info)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<msg>.+?)(?:\s*\[(?<proj>[^\]]+)\])?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Tries to parse a single build output line into a BuildError.
    /// Returns null if the line is not a diagnostic.
    /// </summary>
    public static BuildError? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var m = _msbuildDiag.Match(line);
        if (m.Success)
            return BuildErrorFromMatch(m, hasLocation: true, hasCol: true);

        m = _msbuildNocol.Match(line);
        if (m.Success)
            return BuildErrorFromMatch(m, hasLocation: true, hasCol: false);

        m = _msbuildNoLoc.Match(line);
        if (m.Success)
            return BuildErrorFromMatch(m, hasLocation: false, hasCol: false);

        return null;
    }

    private static BuildError BuildErrorFromMatch(System.Text.RegularExpressions.Match m, bool hasLocation, bool hasCol)
    {
        var severity = m.Groups["severity"].Value.ToLowerInvariant() switch
        {
            "warning" => "Warning",
            "info"    => "Info",
            _         => "Error"
        };

        var filePath = hasLocation ? m.Groups["file"].Value.Trim() : null;
        var projRaw  = m.Groups["proj"].Success ? m.Groups["proj"].Value.Trim() : null;

        return new BuildError
        {
            Severity    = severity,
            Code        = m.Groups["code"].Value.Trim(),
            Message     = m.Groups["msg"].Value.Trim(),
            FilePath    = filePath,
            Line        = hasLocation ? int.Parse(m.Groups["line"].Value) : 0,
            Column      = (hasLocation && hasCol) ? int.Parse(m.Groups["col"].Value) : 0,
            ProjectName = ResolveProjectName(projRaw),
            Category    = "Build"
        };
    }

    private static string? ResolveProjectName(string? projPath)
    {
        if (string.IsNullOrEmpty(projPath)) return null;
        return System.IO.Path.GetFileNameWithoutExtension(projPath);
    }
}

public class BuildError
{
    public string Severity    { get; set; } = "Error";
    public string Code        { get; set; } = "";
    public string Message     { get; set; } = "";
    public string? FilePath   { get; set; }
    public int    Line        { get; set; }
    public int    Column      { get; set; }
    public string? ProjectName { get; set; }
    public string Category    { get; set; } = "Build";
}
