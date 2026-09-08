using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Scadix.AxamlDom;
using System.ComponentModel;
using System.Globalization;

namespace Scadix.AxamlDom.Converters
{
    /// <summary>
    /// TypeConverter for Avalonia image types (IImage, Bitmap)
    /// Converts from string paths/URIs to appropriate image objects
    /// </summary>
    public class BitmapConverter : TypeConverter
    {
        #region Conversion Checks

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        #endregion

        #region Conversion Methods

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str)) return null;

                try
                {
                    var stream = OpenStreamInternal(str, context);
                    if (stream != null)
                    {
                        using (stream)
                        {
                            return new Bitmap(stream);
                        }
                    }
                    return null;
                }
                catch
                {
                    return null; // Return null on error for strings
                }
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                if (value == null) return string.Empty;
                return value.ToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        #endregion

        #region Internal Static Methods

        internal static Stream OpenStreamInternal(string path, ITypeDescriptorContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return null;

                // Normalize path separators
                path = path.Replace("\\", "/");

                // Handle relative paths (e.g. /Assets/logo.ico or Assets/logo.ico)
                if (!path.Contains("://") && !Path.IsPathRooted(path))
                {
                    // 1. Try to resolve using physical file system if project root is known
                    string root = GetProjectRootPath(context);
                    if (!string.IsNullOrEmpty(root))
                    {
                        // Common resource folders to check
                        string[] searchFolders = { "Assets", "Resources", "Resources/Assets", "Properties", "" };
                        var normalizedPath = path.TrimStart('/');

                        foreach (var folder in searchFolders)
                        {
                            var fullPath = string.IsNullOrEmpty(folder)
                                ? Path.Combine(root, normalizedPath)
                                : Path.Combine(root, folder, normalizedPath);

                            if (File.Exists(fullPath))
                            {
                                return File.OpenRead(fullPath);
                            }
                        }
                    }

                    // 2. Try to resolve as avares:// using current project assembly
                    string assemblyName = GetCurrentProjectAssemblyName(context);
                    if (!string.IsNullOrEmpty(assemblyName))
                    {
                        var prefix = path.StartsWith("/") ? "" : "/";
                        var avaresUri = new Uri($"avares://{assemblyName}{prefix}{path}");
                        try { return AssetLoader.Open(avaresUri); } catch { }
                    }
                }

                // 3. Try as absolute URI (e.g. avares://, file://)
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
                {
                    if (uri.Scheme == "avares")
                    {
                        // ⭐ Fix for designer: Resolve avares:// to local file if it's the current project
                        string host = uri.Host;
                        string root = GetProjectRootPath(context);
                        string assemblyName = GetCurrentProjectAssemblyName(context);

                        if (!string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(assemblyName) &&
                            string.Equals(host, assemblyName, StringComparison.OrdinalIgnoreCase))
                        {
                            var localPath = Path.Combine(root, uri.AbsolutePath.TrimStart('/'));
                            if (File.Exists(localPath))
                                return File.OpenRead(localPath);
                        }

                        return AssetLoader.Open(uri);
                    }

                    if (uri.IsFile)
                    {
                        return File.OpenRead(uri.LocalPath);
                    }
                }

                // 4. Try as local file path directly (absolute or relative to current dir)
                if (File.Exists(path))
                {
                    return File.OpenRead(path);
                }
            }
            catch { }

            return null;
        }

        #endregion

        #region Private Static Methods

        private static string GetProjectRootPath(ITypeDescriptorContext context)
        {
            var xamlObj = GetXamlObjectFromContext(context);
            return DesignerProjectContext.CurrentProjectPath;
        }

        private static string GetCurrentProjectAssemblyName(ITypeDescriptorContext context)
        {
            var xamlObj = GetXamlObjectFromContext(context);
            return DesignerProjectContext.CurrentProjectName;
        }

        private static XamlObject GetXamlObjectFromContext(ITypeDescriptorContext context)
        {
            if (context is IServiceProvider sp)
            {
                try
                {
                    // Try to get XamlObject directly
                    var xamlObjProp = sp.GetType().GetProperty("XamlObject");
                    if (xamlObjProp != null) return xamlObjProp.GetValue(sp) as XamlObject;

                    // Try via IProvideValueTarget
                    var pvt = sp.GetService(typeof(Avalonia.Markup.Xaml.IProvideValueTarget));
                    if (pvt != null)
                    {
                        xamlObjProp = pvt.GetType().GetProperty("XamlObject");
                        if (xamlObjProp != null) return xamlObjProp.GetValue(pvt) as XamlObject;
                    }
                }
                catch { }
            }
            return null;
        }

        #endregion
    }
}
