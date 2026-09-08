 

using Avalonia.Controls;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.Extensions
{
	public partial class WrapItemContextMenu: ContextMenu
    {
		private DesignItem designItem;

		public WrapItemContextMenu(DesignItem designItem)
		{
			this.designItem = designItem;
			
			InitializeComponent();
		}

		void Click_WrapInViewbox(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			ModelTools.WrapItemsNewContainer(this.designItem.Services.Selection.SelectedItems, typeof(Viewbox));
		}
	}
}
