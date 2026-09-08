 

using Avalonia;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.Services
{
	sealed class DefaultViewService : ViewService
	{
		readonly DesignContext context;
		
		public DefaultViewService(DesignContext context)
		{
			this.context = context;
		}
		
		public override DesignItem GetModel(AvaloniaObject view)
		{
			// In the WPF designer, we do not support having a different view for a component
			return context.Services.Component.GetDesignItem(view);
		}
	}
}
