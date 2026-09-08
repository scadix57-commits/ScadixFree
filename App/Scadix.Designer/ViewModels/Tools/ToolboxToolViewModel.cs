using Dock.Model.Mvvm.Controls;

namespace Scadix.Designer.ViewModels;

public class ToolboxViewModel : Tool
{
    public ToolboxViewModel() { Id = "Toolbox"; Title = "Toolbox"; CanClose = false; }
}

