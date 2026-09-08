

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Services;

namespace Scadix.AxamlDesigner.Extensions
{
	public class PartialPanelSelectionHandler : BehaviorExtension, IHandlePointerToolMouseDown
	{
		protected override void OnInitialized()
		{
			base.OnInitialized();
			this.ExtendedItem.AddBehavior(typeof(IHandlePointerToolMouseDown), this);
		}
		
		#region IHandlePointerToolMouseDown

		public void HandleSelectionMouseDown(IDesignPanel designPanel, PointerPressedEventArgs e, DesignPanelHitTestResult result)
		{
			var props = e.GetCurrentPoint(null).Properties;
			if (props.IsLeftButtonPressed && MouseGestureBase.IsOnlyButtonPressed(e, MouseButton.Left))
			{
				e.Handled = true;
				new PartialRangeSelectionGesture(result.ModelHit).Start(designPanel, e);
			}
		}
		
		#endregion
	}

	/// <summary>
	/// 
	/// </summary>
	internal class PartialRangeSelectionGesture : RangeSelectionGesture
	{
		public PartialRangeSelectionGesture(DesignItem container)
			: base(container)
		{
		}

		protected override ICollection<DesignItem> GetChildDesignItemsInContainer(Geometry geometry)
		{
			HashSet<DesignItem> resultItems = new HashSet<DesignItem>();
			ViewService viewService = container.Services.View;

			// In Avalonia, use bounds-based approach for partial selection
			void CollectItemsInBounds(Avalonia.Visual visual, Rect bounds)
			{
				if (visual == null) return;
				var model = viewService.GetModel(visual as AvaloniaObject);
				if (model != null && model != container) {
					var visualBounds = visual.Bounds;
					// Include items that are fully inside OR intersect the selection rect
					if (bounds.Intersects(visualBounds)) {
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
	}
}
