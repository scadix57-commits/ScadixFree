using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Scadix.AxamlDom;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
    public partial class ImagePickerControl : UserControl
    {
        // ======== نموذج بيانات الصور ========
        public class ImageItem
        {
            public string FilePath { get; set; }
            public string FileName => Path.GetFileName(FilePath);
            public Bitmap Thumbnail { get; set; }
        }

        // ======== النتيجة ========
        public string SelectedImagePath { get; private set; }
        public string SelectedImageFullPath { get; private set; }

        // ======== حقول خاصة ========
        private string _currentFolder;
        private readonly ObservableCollection<ImageItem> _images = new();

        private static readonly string[] ImageExtensions =
            { ".png", ".jpg", ".jpeg", ".bmp", ".ico", ".gif", ".webp" };

        public ImagePickerControl()
        {
            InitializeComponent();

            var listBox = this.FindControl<ListBox>("uxImageList");
            if (listBox != null) listBox.ItemsSource = _images;

            // التحميل التلقائي عند بدء التشغيل
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var assetsFolder = FindAssetsFolder();
                if (!string.IsNullOrEmpty(assetsFolder))
                    LoadFolder(assetsFolder);
            });
        }

        

        private string FindAssetsFolder()
        {
            var xamlFilePath = DesignerProjectContext.CurrentXamlFilePath;
            if (!string.IsNullOrEmpty(xamlFilePath))
            {
                var found = SearchUpForAssets(Path.GetDirectoryName(xamlFilePath));
                if (found != null) return found;
            }

            var projectPath = DesignerProjectContext.CurrentProjectPath;
            if (!string.IsNullOrEmpty(projectPath))
            {
                var projectDir = Path.GetDirectoryName(projectPath);
                var found = SearchUpForAssets(projectDir);
                if (found != null) return found;

                var direct = Path.Combine(projectDir, "Assets");
                try { Directory.CreateDirectory(direct); } catch { }
                return direct;
            }

            return null;
        }

        private string SearchUpForAssets(string startDir)
        {
            if (string.IsNullOrEmpty(startDir)) return null;

            var dir = new DirectoryInfo(startDir);
            int depth = 0;
            while (dir != null && depth < 8)
            {
                var assets = Path.Combine(dir.FullName, "Assets");
                if (Directory.Exists(assets)) return assets;

                dir = dir.Parent;
                depth++;
            }
            return null;
        }

        private void LoadFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            _currentFolder = folderPath;
            var pathBox = this.FindControl<TextBox>("uxFolderPath");
            if (pathBox != null) pathBox.Text = folderPath;

            _images.Clear();

            var files = Directory.GetFiles(folderPath)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f);

            foreach (var file in files)
            {
                var item = new ImageItem { FilePath = file };
                try
                {
                    using var strm = File.OpenRead(file);
                    item.Thumbnail = new Bitmap(strm);
                }
                catch { }
                _images.Add(item);
            }
        }

        private async void OnBrowseFolderClick(object sender, RoutedEventArgs e)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage == null) return;

            var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "اختر مجلد الصور"
            });

            if (result?.Count > 0)
                LoadFolder(result[0].Path.LocalPath);
        }

        private async void OnBrowseFileClick(object sender, RoutedEventArgs e)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage == null) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "اختر صورة",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.ico", "*.gif", "*.webp" }
                    }
                }
            });

            if (files?.Count > 0)
            {
                var filePath = files[0].Path.LocalPath;
                SetPreview(filePath);
                UpdateSelectedPath(filePath);
            }
        }

        private void OnImageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox lb && lb.SelectedItem is ImageItem item)
            {
                SetPreview(item.FilePath);
                UpdateSelectedPath(item.FilePath);
            }
        }

        private void SetPreview(string filePath)
        {
            var preview = this.FindControl<Image>("uxPreview");
            var nameBlock = this.FindControl<TextBlock>("uxFileName");
            var sizeBlock = this.FindControl<TextBlock>("uxFileSize");

            try
            {
                using var strm = File.OpenRead(filePath);
                if (preview != null) preview.Source = new Bitmap(strm);
                if (nameBlock != null) nameBlock.Text = Path.GetFileName(filePath);
                var info = new FileInfo(filePath);
                if (sizeBlock != null) sizeBlock.Text = $"{info.Length / 1024.0:F1} KB";
            }
            catch (Exception ex)
            {
                if (preview != null) preview.Source = null;
                if (nameBlock != null) nameBlock.Text = $"\u062e\u0637\u0623: {ex.Message}";
            }
        }

        private void UpdateSelectedPath(string fullPath)
        {
            SelectedImageFullPath = fullPath;

            var projectPath = DesignerProjectContext.CurrentProjectPath;
            var projectName = DesignerProjectContext.CurrentProjectName;

            if (!string.IsNullOrEmpty(projectPath))
            {
                try
                {
                    var projectDir = Path.GetDirectoryName(projectPath);
                    var rel = Path.GetRelativePath(projectDir, fullPath).Replace("\\", "/");
                    if (!string.IsNullOrEmpty(projectName))
                        SelectedImagePath = $"avares://{projectName}/{rel}";
                    else
                        SelectedImagePath = rel;
                }
                catch { SelectedImagePath = fullPath; }
            }
            else
            {
                SelectedImagePath = fullPath;
            }

            var pathLabel = this.FindControl<TextBlock>("uxSelectedPath");
            if (pathLabel != null) pathLabel.Text = SelectedImagePath;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedImageFullPath)) return;

            // هل نسخ إلى Assets؟
            var copyCheck = this.FindControl<CheckBox>("uxCopyToAssets");
            if (copyCheck?.IsChecked == true)
            {
                try
                {
                    var assetsFolder = FindAssetsFolder();
                    if (!string.IsNullOrEmpty(assetsFolder))
                    {
                        var destFile = Path.Combine(assetsFolder, Path.GetFileName(SelectedImageFullPath));
                        if (destFile != SelectedImageFullPath)
                            File.Copy(SelectedImageFullPath, destFile, overwrite: true);

                        var projectPath = DesignerProjectContext.CurrentProjectPath;
                        var projectName = DesignerProjectContext.CurrentProjectName;

                        if (!string.IsNullOrEmpty(projectPath))
                        {
                            var projectDir = Path.GetDirectoryName(projectPath);
                            var rel = Path.GetRelativePath(projectDir, destFile).Replace("\\", "/");

                            // ⭐ Use avares:// format if ProjectName is available
                            if (!string.IsNullOrEmpty(projectName))
                                SelectedImagePath = $"avares://{projectName}/{rel}";
                            else
                                SelectedImagePath = rel;
                        }
                        else
                        {
                            SelectedImagePath = "Assets/" + Path.GetFileName(SelectedImageFullPath);
                        }

                        SelectedImageFullPath = destFile;
                    }
                }
                catch (Exception ex)
                {
                    DesignerProjectContext.ReportError(ex, "ImagePickerWindow.CopyImageToAssets");
                }
            }

            
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
           
        }
    }
}
