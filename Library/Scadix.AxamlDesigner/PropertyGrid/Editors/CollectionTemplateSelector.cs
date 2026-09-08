using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
	public class CollectionTemplateSelector : IDataTemplate
	{
		public Control Build(object param)
		{
			return null; // Templates are defined in XAML
		}

		public bool Match(object data)
		{
			return data is DesignItem;
		}
	}
}
