 

using System.IO;
using Avalonia.Markup.Xaml;
using System.Xml;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Themes;
using Scadix.AxamlDesigner.Xaml;
using Scadix.AxamlDom;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia;
using Avalonia.Controls;
using Portable.Xaml;

namespace Scadix.AxamlDesigner.Extensions
{
	public partial class EditStyleContextMenu : ContextMenu
    {
		private DesignItem designItem;

		public EditStyleContextMenu(DesignItem designItem)
		{
			this.designItem = designItem;
			
			InitializeComponent();
		}

		void Click_EditStyle(object sender, RoutedEventArgs e)
		{
			var cg = designItem.OpenGroup("Edit Style");

			var element = designItem.View;
			object defaultStyleKey = element.GetType();
			// TryFindResource in Avalonia 12 requires a ThemeVariant parameter
			object styleObj = null;
			Application.Current?.TryGetResource(defaultStyleKey, null, out styleObj);
			Style style = styleObj as Style;

			var service = ((XamlComponentService) designItem.Services.Component);

			if (style == null) {
				cg.Abort();
				return;
			}

			var ms = new MemoryStream();
			XmlTextWriter writer = new XmlTextWriter(ms, System.Text.Encoding.UTF8);
			writer.Formatting = Formatting.Indented;
			XamlServices.Save(writer, style);

			var rootItem = this.designItem.Context.RootItem as XamlDesignItem;

			ms.Position = 0;
			var sr = new StreamReader(ms);
			var xaml = sr.ReadToEnd();

			var xamlObject = XamlParser.ParseSnippet(rootItem.XamlObject, xaml, ((XamlDesignContext)this.designItem.Context).ParserSettings);
			
			var styleDesignItem=service.RegisterXamlComponentRecursive(xamlObject);
			try {
				designItem.Properties.GetProperty("Resources").CollectionElements.Add(styleDesignItem);
				cg.Commit();
			}
			catch (Exception) {
				cg.Abort();
			}
		}
	}
}
