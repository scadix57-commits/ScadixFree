 

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDesigner.Services;

namespace Scadix.AxamlDesigner.Extensions
{
	
	/// <summary>
	/// The drag handle displayed for Framework Elements
	/// </summary>
	[ExtensionServer(typeof(OnlyOneItemSelectedExtensionServer))]
	[ExtensionFor(typeof(Control))]
	public class TopLeftContainerDragHandle : AdornerProvider
	{
		/// <summary/>
		public TopLeftContainerDragHandle()
		{
			ContainerDragHandle rect = new ContainerDragHandle();
			
			rect.PointerPressed += delegate(object sender, PointerPressedEventArgs e) {
				//Services.Selection.SetSelectedComponents(new DesignItem[] { this.ExtendedItem }, SelectionTypes.Auto);
				new DragMoveMouseGesture(this.ExtendedItem, false).Start(this.ExtendedItem.Services.DesignPanel,e);
				e.Handled=true;
			};
			
			RelativePlacement p = new RelativePlacement(HorizontalAlignment.Left, VerticalAlignment.Top);
			p.XOffset = -7;
			p.YOffset = -7;
			
			AddAdorner(p, AdornerOrder.Background, rect);
		}
	}
	
}

