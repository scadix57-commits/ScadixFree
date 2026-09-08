 

using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Avalonia;

namespace Scadix.AxamlDesigner.Extensions
{
	public class RasterPlacementBehavior : DefaultPlacementBehavior
	{
		Canvas surface;
		AdornerPanel adornerPanel;
		bool rasterDrawn = false;
		int raster = 5;

		public override void BeginPlacement(PlacementOperation operation)
		{
			base.BeginPlacement(operation);
			
			DesignPanel designPanel = ExtendedItem.Services.DesignPanel as DesignPanel;
			if (designPanel != null)
				raster = designPanel.RasterWidth;
			
			CreateSurface(operation);
		}

		public override void EndPlacement(PlacementOperation operation)
		{
			base.EndPlacement(operation);
			DeleteSurface();
		}

		public override void EnterContainer(PlacementOperation operation)
		{
			base.EnterContainer(operation);
			CreateSurface(operation);
		}

		public override void LeaveContainer(PlacementOperation operation)
		{
			base.LeaveContainer(operation);
			DeleteSurface();
		}

		void CreateSurface(PlacementOperation operation)
		{
			if (ExtendedItem.Services.GetService<IDesignPanel>() != null)
			{
				surface = new Canvas();
				adornerPanel = new AdornerPanel();
				adornerPanel.SetAdornedElement(ExtendedItem.View, ExtendedItem);
				AdornerPanel.SetPlacement(surface, AdornerPlacement.FillContent);
				adornerPanel.Children.Add(surface);
				ExtendedItem.Services.DesignPanel.Adorners.Add(adornerPanel);
			}
		}

		void DeleteSurface()
		{
			rasterDrawn = false;
			if (surface != null)
			{
				ExtendedItem.Services.DesignPanel.Adorners.Remove(adornerPanel);
				adornerPanel = null;
				surface = null;
			}
		}

		// Track last key modifiers
		internal KeyModifiers _lastKeyModifiers = KeyModifiers.None;

		public override void BeforeSetPosition(PlacementOperation operation)
		{
			base.BeforeSetPosition(operation);
			if (surface == null) return;

			DesignPanel designPanel = ExtendedItem.Services.DesignPanel as DesignPanel;
			if (designPanel == null || !designPanel.UseRasterPlacement)
				return;

			if ((_lastKeyModifiers & KeyModifiers.Control) != 0)
			{
				surface.Children.Clear();
				rasterDrawn = false;
				return;
			}
			
			drawRaster();

			var bounds = operation.PlacedItems[0].Bounds;
			double newY = ((int)bounds.Y / raster) * raster;
			double newX = ((int)bounds.X / raster) * raster;
			double newW = Convert.ToInt32((bounds.Width / raster)) * raster;
			double newH = Convert.ToInt32((bounds.Height / raster)) * raster;
			operation.PlacedItems[0].Bounds = new Rect(newX, newY, newW, newH);
		}

		public override Point PlacePoint(Point point)
		{
			if (surface == null)
				return base.PlacePoint(point);

			DesignPanel designPanel = ExtendedItem.Services.DesignPanel as DesignPanel;
			if (designPanel == null || !designPanel.UseRasterPlacement)
				return base.PlacePoint(point);

			if ((_lastKeyModifiers & KeyModifiers.Control) != 0)
			{
				surface.Children.Clear();
				rasterDrawn = false;
				return base.PlacePoint(point);
			}

			drawRaster();

			double px = ((int)point.X / raster) * raster;
			double py = ((int)point.Y / raster) * raster;

			return new Point(px, py);
		}

		private void drawRaster()
		{
			if (!rasterDrawn)
			{
				rasterDrawn = true;

				var w = ModelTools.GetWidth(ExtendedItem.View);
				var h = ModelTools.GetHeight(ExtendedItem.View);
				var dash = new AvaloniaList<double>() { 1, raster - 1 };
				for (int i = 0; i <= h; i += raster)
				{
					var line = new Line()
					{
						StartPoint = new Point(0, i),
						EndPoint = new Point(w, i),
						StrokeThickness = 1,
						Stroke = Brushes.Black,
						StrokeDashArray = dash,
					};
					surface.Children.Add(line);
				}
			}
		}
	}
}
