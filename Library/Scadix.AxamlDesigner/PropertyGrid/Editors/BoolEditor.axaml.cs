
using Avalonia.Controls;
using Scadix.AxamlDesign.PropertyGrid;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
	[TypeEditor(typeof(bool))]
	public partial class BoolEditor : UserControl
    {
		public BoolEditor()
		{
			InitializeComponent();
		}
	}
}
