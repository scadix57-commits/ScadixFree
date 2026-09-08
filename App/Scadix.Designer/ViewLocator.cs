using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Scadix.Designer.ViewModels;
using Scadix.Designer.ViewModels.Tools;
using Scadix.Designer.Views;

namespace Scadix.Designer;

public sealed class ViewLocator : IDataTemplate
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Control> _views = new();

    public bool Match(object? data) => data is ViewModelBase || data is Dock.Model.Core.IDockable;

    public Control? Build(object? data)
    {
        if (data is null) return null;

        if (_views.TryGetValue(data, out var cachedView))
            return cachedView;

        Control newView = data switch
        {
            ToolboxViewModel      => new ToolboxView(),
            OutlineViewModel      => new OutlineToolView(),
            PropertiesViewModel   => new PropertiesToolView(),
            ErrorsViewModel       => new ProblemsToolView(),
            ThumbnailViewModel    => new ThumbnailToolView(),
            SolutionViewModel     => new SolutionExplorerView(),
            GitChangesViewModel   => new Views.Tools.GitChangesView(),
          
         
            // ── Debugger tool panels ──────────────────────────────────────
            BreakpointsViewModel    => new Views.Tools.BreakpointsView(),
            CallStackViewModel      => new Views.Tools.CallStackView(),
            LocalsViewModel         => new Views.Tools.LocalsView(),
            WatchersViewModel       => new Views.Tools.WatchersView(),
            DebugSettingsViewModel  => new Views.Tools.DebugSettingsView(),
            DiagnosticToolViewModel => new Views.Tools.DiagnosticToolView(),
            BuildOutputToolViewModel => new Views.Tools.BuildOutputToolView(),
            // ── New tools ─────────────────────────────────────────────────
            TerminalViewModel                 => new Views.Tools.TerminalView(),
            LogicalTreeViewModel              => new Views.Tools.LogicalTreeView(),
            FileExplorerViewModel             => new Views.Tools.FileExplorerView(),
            FindAllReferencesViewModel        => new Views.Tools.FindAllReferencesView(),
            PackageManagerConsoleViewModel    => new Views.Tools.PackageManagerConsoleView(),
            // NuGet Package Manager as a center dock tab
            NuGetDocumentViewModel vm => new NuGetPackageManagerView { DataContext = vm.NuGetViewModel },
            // Git Diff as a center dock tab
            GitDiffDocumentViewModel diffVm => new Views.Documents.GitDiffView { DataContext = diffVm },
            // Git Repositories (Branches + Log) as a center dock tab
            GitRepositoriesDocumentViewModel repoVm => new Views.Tools.GitRepositoriesView { DataContext = repoVm.GitViewModel },
          
            
            DocumentViewModel vm  => new DocumentView { DataContext = vm.Document },
            _ => new TextBlock { Text = $"[ViewLocator] No view for {data.GetType().Name}" }
        };

        if (newView != null && newView.DataContext == null)
        {
            newView.DataContext = data;
        }

        _views.Add(data, newView!);
        return newView;
    }
}
