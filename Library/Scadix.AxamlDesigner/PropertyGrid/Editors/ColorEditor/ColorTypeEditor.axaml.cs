 
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System.ComponentModel;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.PropertyGrid;
using Scadix.AxamlDesigner.PropertyGrid.Editors.BrushEditor;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors.ColorEditor
{
	[TypeEditor(typeof(IImmutableSolidColorBrush))]
	public partial class ColorTypeEditor: UserControl
    {
		public ColorTypeEditor()
		{
			InitializeComponent();
		}

		private ChangeGroup _changeGroup = null;
        private void BackgroundColorView_OnColorChanged(object? sender, ColorChangedEventArgs e)
        {
            try
            {
                var propertyNode = DataContext as PropertyNode;
                if (propertyNode == null)
                {

                    return;
                }

                var color = e.NewColor;
                propertyNode.Value = new ImmutableSolidColorBrush(color);


            }
            catch (Exception)
            {
                // ignored
            }

        }
        
	}
}