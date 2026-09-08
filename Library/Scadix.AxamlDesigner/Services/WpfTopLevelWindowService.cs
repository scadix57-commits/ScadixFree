 

using Avalonia.Controls;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.Services
{
	sealed class WpfTopLevelWindowService : ITopLevelWindowService
	{
		public ITopLevelWindow GetTopLevelWindow(Control element)
		{
			Window window = TopLevel.GetTopLevel(element) as Window;
			if (window != null)
				return new WpfTopLevelWindow(window);
			else
				return null;
		}
		
		sealed class WpfTopLevelWindow : ITopLevelWindow
		{
			Window window;
			
			public WpfTopLevelWindow(Window window)
			{
				this.window = window;
			}
			
			public void SetOwner(Window child)
			{
				// In Avalonia, owner is set differently
				// child.Owner is not directly settable in the same way
			}
			
			public bool Activate()
			{
				window.Activate();
				return true;
			}
		}
	}
}
