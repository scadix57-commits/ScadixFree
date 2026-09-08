using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace Scadix.Designer.ViewModels.Tools;

public partial class FileExplorerViewModel : Tool
{
    public static FileExplorerViewModel? Current { get; private set; }

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public HierarchicalTreeDataGridSource<FileItemViewModel> Source { get; }
    private ObservableCollection<FileItemViewModel> _rootNodes;

    public FileExplorerViewModel()
    {
        Id = "FileExplorer";
        Title = "File Explorer";
        CanClose = true;
        Current = this;

        _rootNodes = new ObservableCollection<FileItemViewModel>();
        Source = new HierarchicalTreeDataGridSource<FileItemViewModel>(_rootNodes)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<FileItemViewModel>(
                    new TemplateColumn<FileItemViewModel>(
                        "Name",
                        "FileItemNameCell",
                        "FileItemNameCell",
                        new GridLength(1, GridUnitType.Star)),
                    x => x.Children,
                    x => x.HasChildren,
                    x => x.IsExpanded)
            }
        };
    }

    public void LoadProject(string? path)
    {
        _rootNodes.Clear();

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            CurrentPath = string.Empty;
            return;
        }

        CurrentPath = path;

        var root = new FileItemViewModel(path, true);
        _rootNodes.Add(root);
        root.IsExpanded = true;
    }

    [RelayCommand]
    private void Refresh() => LoadProject(CurrentPath);

    [RelayCommand]
    private void OpenInExplorer()
    {
        var path = string.IsNullOrEmpty(CurrentPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : CurrentPath;
        try { System.Diagnostics.Process.Start("explorer.exe", path); } catch { }
    }

    [RelayCommand]
    private void NavigateToProject()
    {
        var tree = MainWindowViewModel.Instance?.SolutionTree;
        if (tree != null && tree.Count > 0)
        {
            var fp = tree[0].FilePath;
            var dir = fp != null && File.Exists(fp) ? Path.GetDirectoryName(fp) : fp;
            LoadProject(dir);
        }
    }

    internal void Clear()
    { 
    }
}

public partial class FileItemViewModel : ObservableObject
{
    private string _fullPath;
    private bool _isFolder;

    [ObservableProperty]
    private string _name = string.Empty;

    public string IconKey
    {
        get
        {
            if (_isFolder) return "FolderIcon";
            var ext = Path.GetExtension(_fullPath).ToLowerInvariant();
            return ext switch
            {
                ".cs" => "CSharpFileIcon",
                ".xaml" or ".axaml" => "XamlFileIcon",
                ".csproj" => "ProjectIcon",
                ".sln" => "SolutionIcon",
                ".json" => "JsonFileIcon",
                ".xml" or ".config" => "XmlFileIcon",
                ".txt" or ".md" => "TextFileIcon",
                ".png" or ".jpg" or ".jpeg" or ".ico" or ".bmp" => "ImageFileIcon",
                _ => "ReferenceIcon"
            };
        }
    }

    [ObservableProperty]
    private bool _isExpanded;

    private ObservableCollection<FileItemViewModel>? _children;
    public ObservableCollection<FileItemViewModel>? Children
    {
        get
        {
            if (_isFolder && _children == null)
            {
                _children = new ObservableCollection<FileItemViewModel>();
                LoadChildren();
            }
            return _children;
        }
    }

    public bool HasChildren => _isFolder;

    public FileItemViewModel(string path, bool isFolder)
    {
        _fullPath = path;
        _isFolder = isFolder;
        Name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(Name)) Name = path; // Handle drive roots
    }

    private void LoadChildren()
    {
        if (!_isFolder || _children == null) return;

        try
        {
            _children.Clear();
            var directories = Directory.GetDirectories(_fullPath)
                .OrderBy(x => x)
                .Select(x => new FileItemViewModel(x, true));

            var files = Directory.GetFiles(_fullPath)
                .OrderBy(x => x)
                .Select(x => new FileItemViewModel(x, false));

            foreach (var dir in directories) _children.Add(dir);
            foreach (var file in files) _children.Add(file);
        }
        catch (UnauthorizedAccessException)
        {
            // Handle access denied
        }
        catch (Exception) { }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && _children == null)
        {
            OnPropertyChanged(nameof(Children));
        }
    }
}
