
using Avalonia.Controls;
using Avalonia.Interactivity;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.Extensions
{
	public partial class WrapItemsContextMenu : ContextMenu
    {
		private DesignItem designItem;
		
		public WrapItemsContextMenu(DesignItem designItem)
		{
			this.designItem = designItem;

			this.InitializeComponent();
			
			
            
        }
		
		 
		void Click_WrapInCanvas(object sender, RoutedEventArgs e)
		{
			ModelTools.WrapItemsNewContainer(this.designItem.Services.Selection.SelectedItems, typeof(Canvas));
		}
		
		void Click_WrapInGrid(object sender, RoutedEventArgs e)
		{
			ModelTools.WrapItemsNewContainer(this.designItem.Services.Selection.SelectedItems, typeof(Grid));
		}
	}
}
