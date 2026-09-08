

using Avalonia.Controls;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.Extensions
{
	public partial class UnwrapItemContextMenu: ContextMenu
    {
		private DesignItem designItem;

		public UnwrapItemContextMenu(DesignItem designItem)
		{
			this.designItem = designItem;
			
			InitializeComponent();
		}

		void Click_Unwrap(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			ModelTools.UnwrapItemsFromContainer(this.designItem.Services.Selection.PrimarySelection);
		}
	}
}
