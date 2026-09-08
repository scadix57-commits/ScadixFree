using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;


namespace Scadix.Designer
{
    /// <summary>
    /// Converts a <see cref="SolutionNode"/> to the DynamicResource key string
    /// that maps to a <see cref="DrawingImage"/> defined in Icons.axaml.
    /// </summary>
    public class SolutionNodeIconKeyConverter : IValueConverter
    {
        public static readonly SolutionNodeIconKeyConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not SolutionNode node) return "TextFileIcon";
            if (node.IsMissing) return "WarningIcon";
            if (node.IsStartup) return "RunIcon";

            return node.Kind switch
            {
                SolutionNodeKind.Solution => "SolutionIcon",
                SolutionNodeKind.SolutionFolder => "FolderOpenIcon",
                SolutionNodeKind.Project => "ProjectIcon",
                SolutionNodeKind.DependenciesGroup => "DependenciesIcon",
                SolutionNodeKind.AnalyzersGroup => "AnalyzerIcon",
                SolutionNodeKind.FrameworksGroup => "FrameworkIcon",
                SolutionNodeKind.PackagesGroup => "PackageIcon",
                SolutionNodeKind.ProjectRefsGroup => "ReferenceIcon",
                SolutionNodeKind.Package => "PackageIcon",
                SolutionNodeKind.ProjectRef => "ProjectIcon",
                SolutionNodeKind.Folder => "FolderIcon",
                SolutionNodeKind.File => GetFileIconKey(node.FilePath),
                _ => "TextFileIcon"
            };
        }

        private static string GetFileIconKey(string? path)
        {
            if (path is null) return "TextFileIcon";
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".axaml" or ".xaml" => "XamlFileIcon",
                ".cs" => "CSharpFileIcon",
                ".fs" or ".vb" => "CSharpFileIcon",
                ".csproj" or ".fsproj" or ".vbproj" => "ProjectIcon",
                ".sln" or ".slnx" => "SolutionIcon",
                ".json" => "JsonFileIcon",
                ".xml" => "XmlFileIcon",
                ".md" or ".txt" => "TextFileIcon",
                ".png" or ".jpg" or ".jpeg" or ".ico" or ".bmp" => "ImageFileIcon",
                ".config" or ".manifest" => "ConfigFileIcon",
                _ => "TextFileIcon"
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a <see cref="SolutionNode"/> directly to an <see cref="Avalonia.Media.IImage"/>
    /// by resolving the icon key from Application.Current.Resources.
    /// Accepts MultiBinding: [SolutionNode, IsStartup] so the image refreshes when IsStartup changes.
    /// </summary>
    public class SolutionNodeIconConverter : IValueConverter, IMultiValueConverter
    {
        public static readonly SolutionNodeIconConverter Instance = new();

        // ── IMultiValueConverter (used in AXAML MultiBinding) ─────────────
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            var node = values.OfType<SolutionNode>().FirstOrDefault();
            if (node == null) return null;
            return Resolve(GetKey(node));
        }

        // ── IValueConverter (fallback single binding) ─────────────────────
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not SolutionNode node) return null;
            return Resolve(GetKey(node));
        }

        private static Avalonia.Media.IImage? Resolve(string key)
        {
            try
            {
                if (Application.Current?.Resources.TryGetResource(key, null, out var res) == true)
                    return res as Avalonia.Media.IImage;
            }
            catch { }
            return null;
        }

        private static string GetKey(SolutionNode node)
        {
            if (node.IsMissing) return "WarningIcon";
            if (node.IsStartup) return "RunIcon";
            return node.Kind switch
            {
                SolutionNodeKind.Solution => "SolutionIcon",
                SolutionNodeKind.SolutionFolder => "FolderOpenIcon",
                SolutionNodeKind.Project => "ProjectIcon",
                SolutionNodeKind.DependenciesGroup => "DependenciesIcon",
                SolutionNodeKind.AnalyzersGroup => "AnalyzerIcon",
                SolutionNodeKind.FrameworksGroup => "FrameworkIcon",
                SolutionNodeKind.PackagesGroup => "PackageIcon",
                SolutionNodeKind.ProjectRefsGroup => "ReferenceIcon",
                SolutionNodeKind.Package => "PackageIcon",
                SolutionNodeKind.ProjectRef => "ProjectIcon",
                SolutionNodeKind.Folder => "FolderIcon",
                SolutionNodeKind.File => GetFileKey(node.FilePath),
                _ => "TextFileIcon"
            };
        }

        private static string GetFileKey(string? path)
        {
            if (path is null) return "TextFileIcon";
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".axaml" or ".xaml" => "XamlFileIcon",
                ".cs" or ".fs" or ".vb" => "CSharpFileIcon",
                ".csproj" or ".fsproj" or ".vbproj" => "ProjectIcon",
                ".sln" or ".slnx" => "SolutionIcon",
                ".json" => "JsonFileIcon",
                ".xml" => "XmlFileIcon",
                ".md" or ".txt" => "TextFileIcon",
                ".png" or ".jpg" or ".jpeg" or ".ico" or ".bmp" => "ImageFileIcon",
                ".config" or ".manifest" => "ConfigFileIcon",
                _ => "TextFileIcon"
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();

        // IMultiValueConverter does not have ConvertBack
        public IList<object?> ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }


    /// <summary>
    /// String to Bool converter for Avalonia
    /// </summary>
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? parameter : BindingOperations.DoNothing;
        }
    }
    public class EnumToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }

    public class CollapsedWhenFalse : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? true : false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FalseWhenZero : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || (int)value == 0)
            {
                return false;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LevelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new Thickness(5 + 19 * (int)value, 0, 5, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EqualConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NotEqualConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return true;
            return value.ToString() != parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BranchColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Avalonia.Media.Brushes.DodgerBlue : Avalonia.Media.Brushes.Gray;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoldIfTrueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class GitStatusBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value?.ToString() ?? "") switch
            {
                "M" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E5C07B")), // modified - amber
                "A" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#5DA656")), // added - green
                "D" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E06C75")), // deleted - red
                "R" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#61AFEF")), // renamed - blue
                "??" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#888888")), // untracked - gray
                _ => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#888888")),
            };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns a foreground brush for the file icon based on git status.
    /// Same colors as GitStatusBackgroundConverter but used for Path.Fill.
    /// </summary>
    public class GitStatusForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value?.ToString() ?? "") switch
            {
                "M" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E5C07B")), // modified - amber
                "A" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#5DA656")), // added - green
                "D" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E06C75")), // deleted - red
                "R" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#61AFEF")), // renamed - blue
                "??" => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#888888")), // untracked - gray
                _ => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#75BEFF")), // default - light blue
            };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ConflictRowColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value
                ? Avalonia.Media.Color.Parse("#3D1A1A")
                : Avalonia.Media.Colors.Transparent;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToExpandArrowConverter : IValueConverter
    {
        // Returns path data for ▾ (expanded) or ▸ (collapsed)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value
                ? "M7,10L12,15L17,10H7Z"   // ▾ down arrow
                : "M10,7L15,12L10,17V7Z";  // ▸ right arrow
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// Simple singleton event bus — fires when an icon URL finishes loading.
    /// NuGetPackageInfo subscribes to force a binding refresh.
    /// </summary>
    public class IconLoadedNotifier
    {
        public static readonly IconLoadedNotifier Instance = new();
        public event Action<string>? IconLoaded;
        public void Notify(string url) => IconLoaded?.Invoke(url);
    }

    /// <summary>
    /// Replicates MyDesigner_Master's behavior of converting a string key (e.g. "CSharpFileIcon") 
    /// to an IImage (e.g. DrawingImage from Icons.axaml).
    /// </summary>
    public class StringToImageConverter : IValueConverter
    {
        public static readonly StringToImageConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string iconKey && !string.IsNullOrEmpty(iconKey))
            {
                try
                {
                    if (Application.Current?.Resources != null)
                    {
                        if (Application.Current.TryFindResource(iconKey, out var resource))
                        {
                            if (resource is Avalonia.Media.IImage image)
                                return image;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns a background brush for the author avatar circle based on the author name.
    /// Each unique name gets a consistent color from a fixed palette.
    /// </summary>
    public class AuthorColorConverter : IValueConverter
    {
        private static readonly string[] Palette =
        [
            "#0078D4", "#107C10", "#C50F1F", "#7719AA", "#CA5010",
            "#038387", "#8764B8", "#00B7C3", "#E74856", "#498205",
        ];

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var name = value?.ToString() ?? "";
            var idx = Math.Abs(name.GetHashCode()) % Palette.Length;
            return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(Palette[idx]));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the first character of the author name (uppercase) for the avatar circle.
    /// </summary>
    public class AuthorInitialConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var name = value?.ToString() ?? "";
            return name.Length > 0 ? name[0].ToString().ToUpperInvariant() : "?";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns a shortened author name: first name only (first word before space).
    /// </summary>
    public class AuthorShortNameConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var name = value?.ToString() ?? "";
            var spaceIdx = name.IndexOf(' ');
            return spaceIdx > 0 ? name[..spaceIdx] : name;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class MissingBrushConverter : IValueConverter
    {
        public static readonly MissingBrushConverter Instance = new();
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (bool)(value ?? false)
                ? Avalonia.Media.Brushes.Red
                : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E"));
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class SolutionNodeKindToBoolConverter : IValueConverter
    {
        public static readonly SolutionNodeKindToBoolConverter Instance = new();
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not SolutionNodeKind kind || parameter == null) return false;
            var targetKind = Enum.Parse<SolutionNodeKind>(parameter.ToString()!);
            return kind == targetKind;
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
      
    }
    public class PathToNameConverter : IValueConverter
    {
        public static readonly PathToNameConverter Instance = new();
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            return value ?? "";
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
