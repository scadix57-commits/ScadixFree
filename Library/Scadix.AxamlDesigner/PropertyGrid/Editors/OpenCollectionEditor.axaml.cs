 
using System.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Scadix.AxamlDesign.PropertyGrid;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
	[TypeEditor(typeof(ICollection))]
	public partial class OpenCollectionEditor : UserControl
	{
		public OpenCollectionEditor()
		{
			InitializeComponent();
		}
		
		void open_Click(object sender, RoutedEventArgs e)
		{
			var node = this.DataContext as PropertyNode;
			var parentWindow = this.GetVisualAncestors().OfType<Window>().FirstOrDefault();
			var editor = new FlatCollectionEditor(parentWindow);
			editor.LoadItemsCollection(node.FirstProperty);
			if (parentWindow != null)
				editor.ShowDialog(parentWindow);
			else
				editor.Show();
		}
	}
}