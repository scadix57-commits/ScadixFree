namespace Scadix.Designer.ViewModels;

/// <summary>
/// Document tab that hosts the Git Repositories view (Branches + Commit log)
/// in the center dock — similar to JetBrains Rider's Git tool window.
/// </summary>
public class GitRepositoriesDocumentViewModel : Dock.Model.Mvvm.Controls.Document
{
    public GitRepositoriesViewModel GitViewModel { get; }

    public GitRepositoriesDocumentViewModel(GitRepositoriesViewModel git)
    {
        Id    = "GitRepositories";
        Title = "Git Repositories";
        GitViewModel = git;
    }
}
