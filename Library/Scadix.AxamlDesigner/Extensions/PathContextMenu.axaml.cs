 

using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Themes;
using Avalonia.Controls;

namespace Scadix.AxamlDesigner.Extensions
{
	public partial class PathContextMenu : ContextMenu
    {
		private DesignItem designItem;

		public PathContextMenu(DesignItem designItem)
		{
			this.designItem = designItem;
			
			InitializeComponent();
		}

		void Click_ConvertToFigures(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			var path = this.designItem.Component as Avalonia.Controls.Shapes.Path;
			
			if (path.Data is StreamGeometry) {
				var sg = path.Data as StreamGeometry;
				// GetFlattenedPathGeometry not available - parse to PathGeometry
				PathGeometry pg;
				try { pg = PathGeometry.Parse(sg.ToString()) ?? new PathGeometry(); }
				catch { pg = new PathGeometry(); }
				var pgDes = designItem.Services.Component.RegisterComponentForDesigner(pg);
				designItem.Properties[Avalonia.Controls.Shapes.Path.DataProperty].SetValue(pgDes);
			}
			else if (path.Data is PathGeometry) {
				var pg = path.Data as PathGeometry;
				var figs = pg.Figures;
				var newPg = new PathGeometry();
				var newPgDes = designItem.Services.Component.RegisterComponentForDesigner(newPg);
				foreach (var fig in figs) {
					newPgDes.Properties[PathGeometry.FiguresProperty].CollectionElements.Add(FigureToDesignItem(fig));
				}
				designItem.Properties[Avalonia.Controls.Shapes.Path.DataProperty].SetValue(newPg);
			}
			
		}
		
		private DesignItem FigureToDesignItem(PathFigure pf)
		{
			var pfDes = designItem.Services.Component.RegisterComponentForDesigner(new PathFigure());
			
			pfDes.Properties[PathFigure.StartPointProperty].SetValue(pf.StartPoint);
			pfDes.Properties[PathFigure.IsClosedProperty].SetValue(pf.IsClosed);
			
			foreach (var s in pf.Segments) {
					pfDes.Properties[PathFigure.SegmentsProperty].CollectionElements.Add(SegmentToDesignItem(s));
				}
			return pfDes;
		}
		
		private DesignItem SegmentToDesignItem(PathSegment s)
		{
			// Clone not available in Avalonia - register the segment directly
			var sDes = designItem.Services.Component.RegisterComponentForDesigner(s);
			
			if (!((PathSegment)s).IsStroked)
				sDes.Properties[PathSegment.IsStrokedProperty].SetValue(((PathSegment)s).IsStroked);
			// IsSmoothJoin not available in Avalonia
				
			if (s is LineSegment) {
				sDes.Properties[LineSegment.PointProperty].SetValue(((LineSegment)s).Point);
			} else if (s is QuadraticBezierSegment) {
				sDes.Properties[QuadraticBezierSegment.Point1Property].SetValue(((QuadraticBezierSegment)s).Point1);
				sDes.Properties[QuadraticBezierSegment.Point2Property].SetValue(((QuadraticBezierSegment)s).Point2);
			} else if (s is BezierSegment) {
				sDes.Properties[BezierSegment.Point1Property].SetValue(((BezierSegment)s).Point1);
				sDes.Properties[BezierSegment.Point2Property].SetValue(((BezierSegment)s).Point2);
				sDes.Properties[BezierSegment.Point3Property].SetValue(((BezierSegment)s).Point3);
			} else if (s is ArcSegment) {
				sDes.Properties[ArcSegment.PointProperty].SetValue(((ArcSegment)s).Point);
				sDes.Properties[ArcSegment.IsLargeArcProperty].SetValue(((ArcSegment)s).IsLargeArc);
				sDes.Properties[ArcSegment.RotationAngleProperty].SetValue(((ArcSegment)s).RotationAngle);
				sDes.Properties[ArcSegment.SizeProperty].SetValue(((ArcSegment)s).Size);
				sDes.Properties[ArcSegment.SweepDirectionProperty].SetValue(((ArcSegment)s).SweepDirection);
			} else if (s is PolyLineSegment) {
				sDes.Properties[PolyLineSegment.PointsProperty].SetValue(((PolyLineSegment)s).Points);
			} else if (s is PolyBezierSegment) {
				sDes.Properties[PolyBezierSegment.PointsProperty].SetValue(((PolyBezierSegment)s).Points);
			}
			return sDes;
		}
	}
}

