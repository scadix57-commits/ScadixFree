 

using Avalonia.Controls;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;

namespace Scadix.AxamlDesigner.Extensions
{
	[ExtensionFor(typeof(Panel))]
	[ExtensionFor(typeof(Border))]
	[ExtensionFor(typeof(ContentControl))]
	[ExtensionFor(typeof(Viewbox))]
	public class PanelMove : PermanentAdornerProvider
	{
		protected override void OnInitialized()
		{
			base.OnInitialized();

			var adornerPanel = new AdornerPanel();
			var adorner = new PanelMoveAdorner(ExtendedItem);
			AdornerPanel.SetPlacement(adorner, AdornerPlacement.FillContent);
			adornerPanel.Children.Add(adorner);
			Adorners.Add(adornerPanel);
		}
	}
}
