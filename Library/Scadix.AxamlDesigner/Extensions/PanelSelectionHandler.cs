 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Point = Avalonia.Point;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesign.Services;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDesigner.Services;
using Avalonia.VisualTree;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// Handles selection multiple controls inside a Panel.
	/// </summary>
	[ExtensionFor(typeof(Panel))]
	public class PanelSelectionHandler : BehaviorExtension, IHandlePointerToolMouseDown
	{
		protected override void OnInitialized()
		{
			base.OnInitialized();
			this.ExtendedItem.AddBehavior(typeof(IHandlePointerToolMouseDown), this);
		}
		
		public void HandleSelectionMouseDown(IDesignPanel designPanel, PointerPressedEventArgs e, DesignPanelHitTestResult result)
		{
			var props = e.GetCurrentPoint(null).Properties;
			if (props.IsLeftButtonPressed && MouseGestureBase.IsOnlyButtonPressed(e, MouseButton.Left)) {
				e.Handled = true;
				new RangeSelectionGesture(result.ModelHit).Start(designPanel, e);
			}
		}
	}
	
	internal class RangeSelectionGesture : ClickOrDragMouseGesture
	{
		protected DesignItem container;
		protected AdornerPanel adornerPanel;
		protected SelectionFrame selectionFrame;
		
		protected GrayOutDesignerExceptActiveArea grayOut;
		
		public RangeSelectionGesture(DesignItem container)
		{
			this.container = container;
			this.positionRelativeTo = container.View;
		}
		
		protected override void OnDragStarted(PointerEventArgs e)
		{
			adornerPanel = new AdornerPanel();
			adornerPanel.SetAdornedElement(container.View, container);
			
			selectionFrame = new SelectionFrame();
			adornerPanel.Children.Add(selectionFrame);
			
			designPanel.Adorners.Add(adornerPanel);
			
			GrayOutDesignerExceptActiveArea.Start(ref grayOut, services, container.View);
		}
		
		protected override void OnMouseMove(object sender, PointerEventArgs e)
		{
			base.OnMouseMove(sender, e);
			if (hasDragStarted) {
				SetPlacement(e.GetPosition(positionRelativeTo as Visual));
			}
		}
		
		protected override void OnMouseUp(object sender, PointerReleasedEventArgs e)
		{
			if (hasDragStarted == false) {
				services.Selection.SetSelectedComponents(new DesignItem [] { container }, SelectionTypes.Auto);
			} else {
				Point endPoint = e.GetPosition(positionRelativeTo as Visual);
				Rect frameRect = new Rect(
					Math.Min(startPoint.X, endPoint.X),
					Math.Min(startPoint.Y, endPoint.Y),
					Math.Abs(startPoint.X - endPoint.X),
					Math.Abs(startPoint.Y - endPoint.Y)
				);
				
				ICollection<DesignItem> items = GetChildDesignItemsInContainer(new RectangleGeometry(frameRect));
				if (items.Count == 0) {
					items.Add(container);
				}

				var filterService = services.GetService<ISelectionFilterService>();
				if (filterService != null)
				{
					items = filterService.FilterSelectedElements(items);
				}

				services.Selection.SetSelectedComponents(items, SelectionTypes.Auto);
			}
			Stop();
		}
		
		protected virtual ICollection<DesignItem> GetChildDesignItemsInContainer(Geometry geometry)
		{
			HashSet<DesignItem> resultItems = new HashSet<DesignItem>();
			ViewService viewService = container.Services.View;
			
			// In Avalonia, we use a simpler bounds-based approach instead of WPF's HitTest with geometry
			void CollectItemsInBounds(Avalonia.Visual visual, Rect bounds)
			{
				if (visual == null) return;
				var model = viewService.GetModel(visual as AvaloniaObject);
				if (model != null && model != container) {
					var visualBounds = visual.Bounds;
					if (bounds.Contains(visualBounds.TopLeft) && bounds.Contains(visualBounds.BottomRight)) {
						resultItems.Add(model);
						return;
					}
				}
				foreach (var child in visual.GetVisualChildren()) {
					CollectItemsInBounds(child, bounds);
				}
			}
			
			if (geometry is RectangleGeometry rectGeom) {
				CollectItemsInBounds(container.View, rectGeom.Rect);
			}
			
			return resultItems;
		}
		
		void SetPlacement(Point endPoint)
		{
			RelativePlacement p = new RelativePlacement();
			p.XOffset = Math.Min(startPoint.X, endPoint.X);
			p.YOffset = Math.Min(startPoint.Y, endPoint.Y);
			p.WidthOffset = Math.Max(startPoint.X, endPoint.X) - p.XOffset;
			p.HeightOffset = Math.Max(startPoint.Y, endPoint.Y) - p.YOffset;
			AdornerPanel.SetPlacement(selectionFrame, p);
		}
		
		protected override void OnStopped()
		{
			if (adornerPanel != null) {
				designPanel.Adorners.Remove(adornerPanel);
				adornerPanel = null;
			}
			GrayOutDesignerExceptActiveArea.Stop(ref grayOut);
			selectionFrame = null;
			base.OnStopped();
		}
	}
}
