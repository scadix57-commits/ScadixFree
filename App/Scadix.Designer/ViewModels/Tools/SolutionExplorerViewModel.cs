using Dock.Model.Mvvm.Controls;

namespace Scadix.Designer.ViewModels;

public class SolutionViewModel : Tool
{
    public SolutionViewModel()
    {
        Id       = "Solution";
        Title    = "Solution Explorer";
        CanClose = false;
    }
}
