 

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// Draws a dotted line around selected Controls.
	/// </summary>
	[ExtensionFor(typeof(Control))]
	public class SelectedElementRectangleExtension : SelectionAdornerProvider
	{
		/// <summary>
		/// Creates a new SelectedElementRectangleExtension instance.
		/// </summary>
		public SelectedElementRectangleExtension()
		{
			Rectangle selectionRect = new Rectangle();
			 
			selectionRect.Stroke = new SolidColorBrush(Color.FromRgb(0x47, 0x47, 0x47));
			selectionRect.StrokeThickness = 1.5;
			selectionRect.IsHitTestVisible = false;

			RelativePlacement placement = new RelativePlacement(HorizontalAlignment.Stretch, VerticalAlignment.Stretch);
			placement.XOffset = -1;
			placement.YOffset = -1;
			placement.WidthOffset = 2;
			placement.HeightOffset = 2;

			this.AddAdorners(placement, selectionRect);
		}
	}
}

