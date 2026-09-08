 

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Avalonia;

namespace Scadix.AxamlDesigner.Extensions
{
	[ExtensionFor(typeof(Control))]
	[ExtensionServer(typeof(MouseOverExtensionServer))]

	public class BorderForMouseOver : AdornerProvider
	{
		readonly AdornerPanel adornerPanel;
		readonly Border border = new Border();

		public BorderForMouseOver()
		{
			adornerPanel = new AdornerPanel();
			adornerPanel.Order = AdornerOrder.Background;
			this.Adorners.Add(adornerPanel);
			border.BorderThickness = new Thickness(1);
			border.BorderBrush = Brushes.DodgerBlue;
			border.Margin = new Thickness(-2);
			AdornerPanel.SetPlacement(border, AdornerPlacement.FillContent);
			adornerPanel.Children.Add(border);
		}

		protected override void OnInitialized()
		{
			base.OnInitialized();

			if (ExtendedItem.Component is Line line)
            { 
				// To display border of Line in correct position.
                border.Margin = new Thickness(
                    line.EndPoint.X < 0 ? line.EndPoint.X : 0,  // left
                    line.EndPoint.Y < 0 ? line.EndPoint.Y : 0,  // top
                    0,  // right
                    0   // bottom
                );
            }
		}
	}
}
