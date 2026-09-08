 
using Avalonia.Controls;
using Avalonia.Interactivity;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.UIExtensions;
using Scadix.AxamlDesigner.PropertyGrid.Editors.FormatedTextEditor;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.Extensions
{
	public partial class TextBlockRightClickContextMenu : ContextMenu
    {
		private DesignItem designItem;

		public TextBlockRightClickContextMenu(DesignItem designItem)
		{
			this.designItem = designItem;

			InitializeComponent();
		}

		void Click_EditFormatedText(object sender, RoutedEventArgs e)
		{
			var parentWindow = ((DesignPanel)designItem.Context.Services.DesignPanel).TryFindParent<Window>();
			var dlg = new Window()
			{
				Content = new FormatedTextEditor(designItem),
				Width = 440,
				Height = 200,
			};

			if (parentWindow != null)
				dlg.ShowDialog(parentWindow);
			else
				dlg.Show();
		}
	}
}
