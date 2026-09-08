namespace Scadix.Designer.ViewModels;

/// <summary>
/// Document tab that hosts the Git tool view in the center dock.
/// </summary>
public class GitDocumentViewModel : Dock.Model.Mvvm.Controls.Document
{
    public GitRepositoriesViewModel GitViewModel { get; }

    public GitDocumentViewModel(GitRepositoriesViewModel vm)
    {
        Id    = "GitCenter";
        Title = "Git";
        GitViewModel = vm;
    }
}
