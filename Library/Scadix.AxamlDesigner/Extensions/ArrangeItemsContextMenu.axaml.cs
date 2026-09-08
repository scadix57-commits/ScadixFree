

using Avalonia.Controls;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.Extensions
{
	public partial class ArrangeItemsContextMenu : ContextMenu
    {
		private DesignItem designItem;
		
		public ArrangeItemsContextMenu(DesignItem designItem)
		{
			this.designItem = designItem;
			
			InitializeComponent();
		}
		
		void Click_ArrangeLeft(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			ModelTools.ArrangeItems(this.designItem.Services.Selection.SelectedItems, ArrangeDirection.Left);
		}

		void Click_ArrangeHorizontalCentered(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			ModelTools.ArrangeItems(this.designItem.Services.Selection.SelectedItems, ArrangeDirection.HorizontalMiddle);
		}

		void Click_ArrangeRight(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			ModelTools.ArrangeItems(this.designItem.Services.Selection.SelectedItems, ArrangeDirection.Right);
		}

		void Click_ArrangeTop(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			ModelTools.ArrangeItems(this.designItem.Services.Selection.SelectedItems, ArrangeDirection.Top);
		}

		void Click_ArrangeVerticalCentered(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			ModelTools.ArrangeItems(this.designItem.Services.Selection.SelectedItems, ArrangeDirection.VerticalMiddle);
		}

		void Click_ArrangeBottom(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			ModelTools.ArrangeItems(this.designItem.Services.Selection.SelectedItems, ArrangeDirection.Bottom);
		}
	}
}
