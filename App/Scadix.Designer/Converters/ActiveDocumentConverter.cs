using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Scadix.Designer.Converters
{
    /// <summary>
    /// Converts a URL string to an Avalonia Bitmap, with in-memory caching and async loading.
    /// Used to display real NuGet package icons in the package list.
    /// </summary>
    public class BitmapValueConverter : IValueConverter
    {
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestHeaders = { { "User-Agent", "MyDesigner-NuGetManager/1.0" } }
        };

        // Cache: URL → Avalonia Bitmap (null = failed/loading)
        private static readonly ConcurrentDictionary<string, Bitmap?> _cache = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string url || string.IsNullOrWhiteSpace(url))
                return null;

            // Return cached bitmap immediately
            if (_cache.TryGetValue(url, out var cached))
                return cached;

            // Mark as loading to avoid duplicate requests
            _cache[url] = null;

            // Load async — when done, notify bindings via IconLoadedNotifier
            _ = Task.Run(async () =>
            {
                try
                {
                    // Check if we are online for http/https URLs
                    bool isRemote = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

                    if (isRemote && !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                    {
                        // Skip download if no network adapter is active
                        return;
                    }

                    byte[] bytes;

                    if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        bytes = await _http.GetByteArrayAsync(url);
                    }
                    else if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = new Uri(url).LocalPath;
                        bytes = await File.ReadAllBytesAsync(path);
                    }
                    else
                    {
                        return;
                    }

                    using var ms = new MemoryStream(bytes);
                    var bmp = new Bitmap(ms);   // Avalonia.Media.Imaging.Bitmap
                    _cache[url] = bmp;

                    // Notify UI to re-render via Avalonia dispatcher
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        IconLoadedNotifier.Instance.Notify(url);
                    });
                }
                catch
                {
                    // Keep null in cache — fallback colored box will show
                    _cache.TryRemove(url, out _);
                }
            });

            return null; // fallback shows until loaded
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();

        /// <summary>Clear the icon cache (e.g. on memory pressure).</summary>
        public static void ClearCache() => _cache.Clear();
    }

    class ActiveDocumentConverter : IValueConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
		{
			if (value is Document)
				return value;

			return AvaloniaProperty.UnsetValue;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
		{
			if (value is Document)
				return value;

			return AvaloniaProperty.UnsetValue;
		}
	}
}
