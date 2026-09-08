 

using System.Reflection;
using Avalonia.Controls.ApplicationLifetimes;

namespace Scadix.AxamlDesigner.Services
{
	public abstract class ChooseClassServiceBase
	{
		public Type ChooseClass()
		{
			var core = new ChooseClass(GetAssemblies());
			var window = new ChooseClassDialog(core);
			
			// ShowDialog requires a parent window in Avalonia
			bool? result = null;
			if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
				&& desktop.MainWindow != null)
			{
				result = window.ShowDialog<bool?>(desktop.MainWindow).GetAwaiter().GetResult();
			}
			
			if (result == true) {
				return core.CurrentClass;
			}
			return null;
		}

		public abstract IEnumerable<Assembly> GetAssemblies();
	}
}
