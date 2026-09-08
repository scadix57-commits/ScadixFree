

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Scadix.AxamlDesign.PropertyGrid;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors.BrushEditor
{
	[TypeEditor(typeof(IBrush))]
	public partial class BrushTypeEditor :UserControl
	{
		public BrushTypeEditor()
		{
			InitializeComponent();
		}
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
