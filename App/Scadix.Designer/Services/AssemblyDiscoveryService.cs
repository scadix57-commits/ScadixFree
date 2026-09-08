
using Scadix.AxamlDom;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Reflection;
using System.Text;

namespace Scadix.Designer.Services
{
    internal class AssemblyDiscoveryService
    {

        private static readonly ConcurrentDictionary<string, List<string>> _pathCache = new();
        private static readonly string[] ForbiddenFolders = {
            "\\ref\\", "\\runtimes\\", "\\native\\", "\\obj\\", "\\resources\\"
            
        };


        /// <summary>
        /// Loads an assembly from a specific path, but only if it's a valid path (not junk).
        /// </summary>
        public static Assembly LoadAssemblyFromPath(string path)
        {
           
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (!IsValidAssemblyPath(path)) return null;

            try
            {
                return Assembly.LoadFrom(path);
            }
            catch (Exception ex)
            {
                ReportError(ex, $"AssemblyDiscovery loading from {path}");
                return null;
            }
        }

        private static void ReportError(Exception ex, string v)
        {
           
        }

        public static string PreferredTFMFolder { get; set; } = string.Empty;

        /// <summary>
        /// Checks if a path is a valid assembly for the designer (not a runtime, native, or language satellite).
        /// </summary>
        public static bool IsValidAssemblyPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            // 1. Check for specific forbidden technical folders
            foreach (var forbidden in ForbiddenFolders)
            {
                if (path.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // 2. Check for ISO language folders (satellite assemblies)
            if (IsLanguageFolder(path))
                return false;

            return true;
        }

        private static readonly HashSet<string> CommonLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "ar", "bg", "ca", "cs", "da", "de", "el", "en", "es", "et", "fi", "fr", "he", "hi", "hr", "hu", "id", "it", "ja", "ko", "lt", "lv", "ms", "nl", "no", "pl", "pt", "ro", "ru", "sk", "sl", "sr", "sv", "th", "tr", "uk", "vi", "zh"
        };

        /// <summary>
        /// Detects if the assembly is inside a language-specific folder (e.g., /ar/, /en-US/).
        /// </summary>
        public static bool IsLanguageFolder(string path)
        {
            try
            {
                var directoryName = Path.GetFileName(Path.GetDirectoryName(path));
                if (string.IsNullOrEmpty(directoryName)) return false;

                // Pattern: ar, en, fr (2 chars) - Only if in the known language codes list
                if (directoryName.Length == 2 && CommonLanguageCodes.Contains(directoryName))
                    return true;

                // Pattern: ar-SA, en-US (5 chars with dash at pos 2)
                if (directoryName.Length == 5 && directoryName[2] == '-' && CommonLanguageCodes.Contains(directoryName.Substring(0, 2)))
                    return true;

                // Pattern: es-419 (6 chars with dash)
                if (directoryName.Length == 6 && directoryName[2] == '-' && CommonLanguageCodes.Contains(directoryName.Substring(0, 2)))
                    return true;
            }
            catch { }
            return false;
        }
    }
}
