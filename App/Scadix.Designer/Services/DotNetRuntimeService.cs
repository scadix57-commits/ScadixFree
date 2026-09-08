using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Scadix.Designer.Services
{
    public class DotNetRuntimeService
    {
        private static readonly Lazy<DotNetRuntimeService> _instance = new(() => new DotNetRuntimeService());
        public static DotNetRuntimeService Instance => _instance.Value;

        private readonly HashSet<string> _installedRuntimes = new(StringComparer.OrdinalIgnoreCase);
        private bool _isInitialized = false;

        private DotNetRuntimeService() { }

        public void EnsureInitialized()
        {
            if (_isInitialized) return;
            RefreshInstalledRuntimes();
            _isInitialized = true;
        }

        public void RefreshInstalledRuntimes()
        {
            _installedRuntimes.Clear();
            try
            {
                var psi = new ProcessStartInfo("dotnet", "--list-runtimes")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Example: Microsoft.NETCore.App 8.0.0 [...]
                    var regex = new Regex(@"Microsoft\.NETCore\.App\s+(\d+\.\d+)");
                    var matches = regex.Matches(output);
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var version = match.Groups[1].Value; // e.g. "8.0"
                            _installedRuntimes.Add(version);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error checking .NET runtimes: {ex.Message}");
            }
        }

        public bool IsRuntimeInstalled(string? targetFramework)
        {
            if (string.IsNullOrEmpty(targetFramework)) return true; // Assume true if unknown

            // net10.0 -> 8.0
            // net10.0-windows -> 10.0
            var match = Regex.Match(targetFramework, @"net(\d+\.\d+)");
            if (match.Success)
            {
                var version = match.Groups[1].Value;
                return _installedRuntimes.Contains(version);
            }

            return true; // Default to true for non-standard TFMs for now
        }
    }
}
