
using Avalonia;
using Avalonia.Controls;
using System.Diagnostics;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner
{
	/// <summary>
	/// Intializes different behaviors for the Root item.
	/// <remarks>Could not be a extension since Root Item is can be of any type</remarks>
	/// </summary>
	public class RootItemBehavior : IRootPlacementBehavior
	{
		private DesignItem _rootItem;
		
		public void Intialize(DesignContext context)
		{
			Debug.Assert(context.RootItem!=null);
			this._rootItem=context.RootItem;
			_rootItem.AddBehavior(typeof(IRootPlacementBehavior),this);
		}

		public bool CanPlace(IEnumerable<DesignItem> childItems, PlacementType type, PlacementAlignment position)
		{
			return type == PlacementType.Resize && (position == PlacementAlignment.Right || position == PlacementAlignment.BottomRight || position == PlacementAlignment.Bottom);
		}
		
		public void BeginPlacement(PlacementOperation operation)
		{
			
		}
		
		public void EndPlacement(PlacementOperation operation)
		{
			
		}
		
		public Rect GetPosition(PlacementOperation operation, DesignItem childItem)
		{
			Control child = childItem.View;
			return new Rect(0, 0, ModelTools.GetWidth(child), ModelTools.GetHeight(child));
		}
		
		public void BeforeSetPosition(PlacementOperation operation)
		{
		}
		
		public void SetPosition(PlacementInformation info)
		{
			Control element = info.Item.View;
			Rect newPosition = info.Bounds;
			if (newPosition.Right != ModelTools.GetWidth(element)) {
				info.Item.Properties[Control.WidthProperty].SetValue(newPosition.Right);
			}
			if (newPosition.Bottom != ModelTools.GetHeight(element)) {
				info.Item.Properties[Control.HeightProperty].SetValue(newPosition.Bottom);
			}
		}
		
		public bool CanLeaveContainer(PlacementOperation operation)
		{
			return false;
		}
		
		public void LeaveContainer(PlacementOperation operation)
		{
			throw new NotImplementedException();
		}
		
		public bool CanEnterContainer(PlacementOperation operation, bool shouldAlwaysEnter)
		{
			return false;
		}
		
		public void EnterContainer(PlacementOperation operation)
		{
			throw new NotImplementedException();
		}

		public Point PlacePoint(Point point)
		{
			return point;
		}
	}
}
