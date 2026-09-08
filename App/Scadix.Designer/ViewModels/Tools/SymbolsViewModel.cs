using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace Scadix.Designer.ViewModels;

public class SymbolsViewModel : Tool
{
    public SymbolsViewModel()
    {
        Id = "Symbols";
        Title = "Symbols";
        CanClose = true;
        DockCapabilityOverrides = new DockCapabilityOverrides { CanClose = true, CanPin = true, CanDrag = true, CanDrop = true };
    }
   
}
