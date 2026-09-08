 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;

namespace Scadix.AxamlDesigner.Extensions
{
	public class PointTrackerPlacementSupport : AdornerPlacement
	{
		private Shape shape;
		private PlacementAlignment alignment;

		public int Index
		{
			get; set;
		}
		
		public PointTrackerPlacementSupport(Shape s, PlacementAlignment align, int index)
		{
			shape = s;
			alignment = align;
			Index = index;
		}

		/// <summary>
		/// Arranges the adorner element on the specified adorner panel.
		/// </summary>
		public override void Arrange(AdornerPanel panel, Control adorner, Size adornedElementSize)
		{
			Point p = new Point(0, 0);
			if (shape is Line)
			{
				var s = shape as Line;
				double x, y;
				
				if (alignment == PlacementAlignment.BottomRight)
				{
					x = s.EndPoint.X;
					y = s.EndPoint.Y;
				}
				else
				{
					x = s.StartPoint.X;
					y = s.StartPoint.Y;
				}
				p = new Point(x, y);
			} else if (shape is Polygon) {
				var pg = shape as Polygon;
				p = pg.Points[Index];
			} else if (shape is Polyline) {
				var pg = shape as Polyline;
				p = pg.Points[Index];
			}

			var transform = shape.RenderedGeometry.Transform;
			p = transform.Value.Transform(p);
			
			adorner.Arrange(new Rect(p.X - 3.5, p.Y - 3.5, 7, 7));
		}
	}
}
