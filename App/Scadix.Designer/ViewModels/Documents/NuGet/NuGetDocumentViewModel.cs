using Dock.Model.Mvvm.Controls;
using Scadix.Designer.ViewModels.NuGet;

namespace Scadix.Designer.ViewModels;

/// <summary>
/// Document tab that hosts the NuGet Package Manager in the center dock.
/// </summary>
public class NuGetDocumentViewModel : Dock.Model.Mvvm.Controls.Document
{
    public NuGetPackageManagerViewModel NuGetViewModel { get; }

    public NuGetDocumentViewModel(NuGetPackageManagerViewModel vm)
    {
        NuGetViewModel = vm;
    }

    public override bool OnClose()
    {
        return base.OnClose();
    }
}
