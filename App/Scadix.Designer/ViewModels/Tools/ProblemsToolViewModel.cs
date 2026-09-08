using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dock.Model.Mvvm.Controls;
using Scadix.Designer.Services;

namespace Scadix.Designer.ViewModels;

public class ErrorsViewModel : Tool
{
    public ErrorsViewModel() 
    { 
        Id = "Errors"; 
        Title = "Errors"; 
        CanClose = false; 
    }

    public ObservableCollection<BuildError> ErrorList { get; } = new();

 

    public void Clear()
    {
        ErrorList.Clear();
    }
}
