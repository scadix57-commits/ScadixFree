 

using System.Reflection;
using Avalonia.Controls;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// 
	/// </summary>
	[ExtensionServer(typeof(OnlyOneItemSelectedExtensionServer))]
	[ExtensionFor(typeof(Grid))]
	[ExtensionFor(typeof(Canvas))]
	[Extension(Order = 51)]
	public class UnwrapItemContextMenuExtension : SelectionAdornerProvider
	{
		DesignPanel panel;
		ContextMenu contextMenu;

		protected override void OnInitialized()
		{
			base.OnInitialized();

			contextMenu = new UnwrapItemContextMenu(ExtendedItem);
			panel = ExtendedItem.Context.Services.DesignPanel as DesignPanel;
			if (panel != null)
				panel.AddContextMenu(contextMenu, this.GetType().GetCustomAttribute<ExtensionAttribute>().Order);
		}

		protected override void OnRemove()
		{
			if (panel != null)
				panel.RemoveContextMenu(contextMenu);

			base.OnRemove();
		}
	}
}
