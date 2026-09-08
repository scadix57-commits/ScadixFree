using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Scadix.Designer.Models.NuGet
{
    public partial class NuGetPackageInfo : ObservableObject
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Authors { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string? InstalledVersion { get; set; }
        public List<string> Versions { get; set; } = new();
        public long DownloadCount { get; set; }
        public string? ProjectUrl { get; set; }
        public bool IsInstalled { get; set; }
        public bool HasUpdate { get; set; }
        public List<string> Tags { get; set; } = new();

        [ObservableProperty]
        private bool _isSelected = true;

        // ── Icon ──────────────────────────────────────────────────────────────

        private string? _iconUrl;
        public string? IconUrl
        {
            get => _iconUrl;
            set
            {
                if (_iconUrl == value) return;
                _iconUrl = value;
                OnPropertyChanged();

                // When async loader finishes, fire PropertyChanged again
                // so the Image binding picks up the cached Bitmap.
                if (!string.IsNullOrEmpty(value))
                    IconLoadedNotifier.Instance.IconLoaded += OnIconLoaded;
            }
        }

        private void OnIconLoaded(string url)
        {
            if (url == _iconUrl)
            {
                IconLoadedNotifier.Instance.IconLoaded -= OnIconLoaded;
                OnPropertyChanged(nameof(IconUrl));
            }
        }
    }
}
