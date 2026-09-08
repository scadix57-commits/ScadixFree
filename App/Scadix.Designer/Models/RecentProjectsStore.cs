using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Scadix.Designer;

/// <summary>
/// Persists RecentProjects to a JSON file in the user's AppData folder.
/// File: %APPDATA%\XAMLStudio\RecentProjects.json
/// </summary>
public static class RecentProjectsStore
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XAMLStudio",
        "RecentProjects.json");

    // ── Serialization DTO ─────────────────────────────────────────────────

    private sealed class EntryDto
    {
        public string Name        { get; set; } = "";
        public string Path        { get; set; } = "";
        public string LastOpened  { get; set; } = "";
        public string IconColor   { get; set; } = "#4D78CC";
        public bool   IsFolder    { get; set; }
    }

    // ── Save ──────────────────────────────────────────────────────────────

    public static void Save()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(dir);

            var dtos = new List<EntryDto>();
            foreach (var entry in MainWindowViewModel.Instance.RecentProjects)
            {
                dtos.Add(new EntryDto
                {
                    Name       = entry.Name,
                    Path       = entry.Path,
                    LastOpened = entry.LastOpened,
                    IconColor  = entry.IconColor,
                    IsFolder   = entry.IsFolder
                });
            }

            var json = JsonSerializer.Serialize(dtos,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { /* never crash on save */ }
    }

    // ── Load ──────────────────────────────────────────────────────────────

    public static void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;

            var json = File.ReadAllText(_filePath);
            var dtos = JsonSerializer.Deserialize<List<EntryDto>>(json);
            if (dtos == null) return;

            MainWindowViewModel.Instance.RecentProjects.Clear();
            foreach (var dto in dtos)
            {
                MainWindowViewModel.Instance.RecentProjects.Add(new RecentProjectEntry
                {
                    Name       = dto.Name,
                    Path       = dto.Path,
                    LastOpened = dto.LastOpened,
                    IconColor  = dto.IconColor,
                    IsFolder   = dto.IsFolder
                });
            }
        }
        catch { /* never crash on load */ }
    }
}
