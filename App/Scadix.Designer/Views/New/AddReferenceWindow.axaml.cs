using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Linq;

namespace Scadix.Designer;

public partial class AddReferenceWindow : Window
{
    public List<SolutionNode> SelectedProjects { get; private set; } = new();

    public AddReferenceWindow()
    {
        InitializeComponent();
    }

    private List<SolutionNode> _allAvailable = new();

    public void LoadProjects(IEnumerable<SolutionNode> allProjects, string currentProjectPath, IEnumerable<string> existingReferences)
    {
        _allAvailable = allProjects
            .Where(p => p.Kind == SolutionNodeKind.Project && 
                        p.FilePath != currentProjectPath && 
                        !existingReferences.Contains(p.FilePath))
            .ToList();
        
        ApplyFilter();
    }

    private string _filter = "";
    public string Filter
    {
        get => _filter;
        set { _filter = value; ApplyFilter(); }
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(_filter))
            ProjectListBox.ItemsSource = _allAvailable;
        else
            ProjectListBox.ItemsSource = _allAvailable
                .Where(p => p.Name.Contains(_filter, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        Filter = SearchBox.Text ?? "";
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        SelectedProjects = ProjectListBox.SelectedItems.Cast<SolutionNode>().ToList();
        if (SelectedProjects.Any())
        {
            Close(true);
        }
    }

    private async void AddFrom_Click(object? sender, RoutedEventArgs e)
    {
        var top = VisualRoot as Window;
        if (top == null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select Project or Assembly",
            AllowMultiple = true,
            FileTypeFilter = new[] { 
                new Avalonia.Platform.Storage.FilePickerFileType("Projects & Assemblies") { Patterns = new[] { "*.csproj", "*.dll" } }
            }
        });

        if (files.Any())
        {
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                // For now just add as a SolutionNode mockup if needed, 
                // but usually we just return the paths or add them directly.
            }
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
