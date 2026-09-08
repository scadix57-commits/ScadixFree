 

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// Provides <see cref="IPlacementBehavior"/> behavior for <see cref="Grid"/>.
	/// </summary>
	[ExtensionFor(typeof(Grid), OverrideExtension=typeof(DefaultPlacementBehavior))]
	public sealed class GridPlacementSupport : SnaplinePlacementBehavior
	{
		Grid grid;
		private bool enteredIntoNewContainer;
		
		protected override void OnInitialized()
		{
			base.OnInitialized();
			grid = (Grid)this.ExtendedItem.Component;
		}
		
		double GetColumnOffset(int index)
		{
			if (index == 0) return 0;
			double offset = 0;
			for (int i = 0; i < index && i < grid.ColumnDefinitions.Count; i++)
				offset += grid.ColumnDefinitions[i].ActualWidth;
			if (index >= grid.ColumnDefinitions.Count)
				return grid.Bounds.Width;
			return offset;
		}
		
		double GetRowOffset(int index)
		{
			if (index == 0) return 0;
			double offset = 0;
			for (int i = 0; i < index && i < grid.RowDefinitions.Count; i++)
				offset += grid.RowDefinitions[i].ActualHeight;
			if (index >= grid.RowDefinitions.Count)
				return grid.Bounds.Height;
			return offset;
		}
		
		const double epsilon = 0.00000001;
		
		int GetColumnIndex(double x)
		{
			if (grid.ColumnDefinitions.Count == 0)
				return 0;
			double offset = 0;
			for (int i = 0; i < grid.ColumnDefinitions.Count; i++) {
				offset += grid.ColumnDefinitions[i].ActualWidth;
				if (x < offset - epsilon)
					return i;
			}
			return grid.ColumnDefinitions.Count - 1;
		}
		
		int GetRowIndex(double y)
		{
			if (grid.RowDefinitions.Count == 0)
				return 0;
			double offset = 0;
			for (int i = 0; i < grid.RowDefinitions.Count; i++) {
				offset += grid.RowDefinitions[i].ActualHeight;
				if (y < offset - epsilon)
					return i;
			}
			return grid.RowDefinitions.Count - 1;
		}
		
		int GetEndColumnIndex(double x)
		{
			if (grid.ColumnDefinitions.Count == 0)
				return 0;
			double offset = 0;
			for (int i = 0; i < grid.ColumnDefinitions.Count; i++) {
				offset += grid.ColumnDefinitions[i].ActualWidth;
				if (x <= offset + epsilon)
					return i;
			}
			return grid.ColumnDefinitions.Count - 1;
		}
		
		int GetEndRowIndex(double y)
		{
			if (grid.RowDefinitions.Count == 0)
				return 0;
			double offset = 0;
			for (int i = 0; i < grid.RowDefinitions.Count; i++) {
				offset += grid.RowDefinitions[i].ActualHeight;
				if (y <= offset + epsilon)
					return i;
			}
			return grid.RowDefinitions.Count - 1;
		}
		
		protected override void AddContainerSnaplines(Rect containerRect, List<SnaplinePlacementBehavior.Snapline> horizontalMap, List<SnaplinePlacementBehavior.Snapline> verticalMap)
		{
			var grid = (Grid)ExtendedItem.View;
			double offset = 0;
			foreach (RowDefinition r in grid.RowDefinitions)
			{
				offset += r.ActualHeight;
				horizontalMap.Add(new Snapline() { RequireOverlap = false, Offset = offset, Start = offset, End = containerRect.Right });
				if (SnaplineMargin > 0)
				{
					horizontalMap.Add(new Snapline() { RequireOverlap = false, Offset = offset - SnaplineMargin, Start = offset, End = containerRect.Right });
					horizontalMap.Add(new Snapline() { RequireOverlap = false, Offset = offset + SnaplineMargin, Start = offset, End = containerRect.Right });
				}

			}
			offset = 0;
			foreach (ColumnDefinition c in grid.ColumnDefinitions)
			{
				offset += c.ActualWidth;
				verticalMap.Add(new Snapline() { RequireOverlap = false, Offset = offset, Start = containerRect.Top, End = containerRect.Bottom });
				if (SnaplineMargin > 0)
				{
					verticalMap.Add(new Snapline() { RequireOverlap = false, Offset = offset - SnaplineMargin, Start = containerRect.Top, End = containerRect.Bottom });					
					verticalMap.Add(new Snapline() { RequireOverlap = false, Offset = offset + SnaplineMargin, Start = containerRect.Top, End = containerRect.Bottom });
				}
			}			
		}
		
		static void SetColumn(DesignItem item, int column, int columnSpan)
		{
			Debug.Assert(item != null && column >= 0 && columnSpan > 0);
			item.Properties.GetAttachedProperty(Grid.ColumnProperty).SetValue(column);
			if (columnSpan == 1) {
				item.Properties.GetAttachedProperty(Grid.ColumnSpanProperty).Reset();
			} else {
				item.Properties.GetAttachedProperty(Grid.ColumnSpanProperty).SetValue(columnSpan);
			}
		}
		
		static void SetRow(DesignItem item, int row, int rowSpan)
		{
			Debug.Assert(item != null && row >= 0 && rowSpan > 0);
			item.Properties.GetAttachedProperty(Grid.RowProperty).SetValue(row);
			if (rowSpan == 1) {
				item.Properties.GetAttachedProperty(Grid.RowSpanProperty).Reset();
			} else {
				item.Properties.GetAttachedProperty(Grid.RowSpanProperty).SetValue(rowSpan);
			}
		}
		
		static HorizontalAlignment SuggestHorizontalAlignment(Rect itemBounds, Rect availableSpaceRect)
		{
			bool isLeft = itemBounds.Left < availableSpaceRect.Left + availableSpaceRect.Width / 4;
			bool isRight = itemBounds.Right > availableSpaceRect.Right - availableSpaceRect.Width / 4;
			if (isLeft && isRight)
				return HorizontalAlignment.Stretch;
			else if (isRight)
				return HorizontalAlignment.Right;
			else
				return HorizontalAlignment.Left;
		}
		
		static VerticalAlignment SuggestVerticalAlignment(Rect itemBounds, Rect availableSpaceRect)
		{
			bool isTop = itemBounds.Top < availableSpaceRect.Top + availableSpaceRect.Height / 4;
			bool isBottom = itemBounds.Bottom > availableSpaceRect.Bottom - availableSpaceRect.Height / 4;
			if (isTop && isBottom)
				return VerticalAlignment.Stretch;
			else if (isBottom)
				return VerticalAlignment.Bottom;
			else
				return VerticalAlignment.Top;
		}
		
		public override void EnterContainer(PlacementOperation operation)
		{
			enteredIntoNewContainer=true;
			grid.UpdateLayout();
			base.EnterContainer(operation);
			
			if (operation.Type == PlacementType.PasteItem) {
				foreach (PlacementInformation info in operation.PlacedItems) {				
					var margin = info.Item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
					var horizontalAlignment = info.Item.Properties.GetProperty(Control.HorizontalAlignmentProperty).GetConvertedValueOnInstance<HorizontalAlignment>();
					var verticalAlignment = info.Item.Properties.GetProperty(Control.VerticalAlignmentProperty).GetConvertedValueOnInstance<VerticalAlignment>();
					
					double mLeft = margin.Left, mTop = margin.Top, mRight = margin.Right, mBottom = margin.Bottom;
					if (horizontalAlignment == HorizontalAlignment.Left)
						mLeft += PlacementOperation.PasteOffset;
					else if (horizontalAlignment == HorizontalAlignment.Right)
						mRight -= PlacementOperation.PasteOffset;
					
					if (verticalAlignment == VerticalAlignment.Top)
						mTop += PlacementOperation.PasteOffset;
					else if (verticalAlignment == VerticalAlignment.Bottom)
						mBottom -= PlacementOperation.PasteOffset;
										
					info.Item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(mLeft, mTop, mRight, mBottom));
				}
			}		
		}
		
		GrayOutDesignerExceptActiveArea grayOut;
		
		public override void EndPlacement(PlacementOperation operation)
		{
			GrayOutDesignerExceptActiveArea.Stop(ref grayOut);
			enteredIntoNewContainer=false;
			base.EndPlacement(operation);
		}
		
		public override void SetPosition(PlacementInformation info)
		{
			base.SetPosition(info);
			int leftColumnIndex = GetColumnIndex(info.Bounds.Left);
			int rightColumnIndex = GetEndColumnIndex(info.Bounds.Right);
			if (rightColumnIndex < leftColumnIndex) rightColumnIndex = leftColumnIndex;
			SetColumn(info.Item, leftColumnIndex, rightColumnIndex - leftColumnIndex + 1);
			int topRowIndex = GetRowIndex(info.Bounds.Top);
			int bottomRowIndex = GetEndRowIndex(info.Bounds.Bottom);
			if (bottomRowIndex < topRowIndex) bottomRowIndex = topRowIndex;
			SetRow(info.Item, topRowIndex, bottomRowIndex - topRowIndex + 1);
			
			Rect availableSpaceRect = new Rect(
				new Point(GetColumnOffset(leftColumnIndex), GetRowOffset(topRowIndex)),
				new Point(GetColumnOffset(rightColumnIndex + 1), GetRowOffset(bottomRowIndex + 1))
			);
			if (info.Item == Services.Selection.PrimarySelection) {
				// only for primary selection:
				if (grayOut != null) {
					grayOut.AnimateActiveAreaRectTo(availableSpaceRect);
				} else {
					GrayOutDesignerExceptActiveArea.Start(ref grayOut, this.Services, this.ExtendedItem.View, availableSpaceRect);
				}
			}
			
			HorizontalAlignment ha = info.Item.Properties[Control.HorizontalAlignmentProperty].GetConvertedValueOnInstance<HorizontalAlignment>();
			VerticalAlignment va = info.Item.Properties[Control.VerticalAlignmentProperty].GetConvertedValueOnInstance<VerticalAlignment>();
			if(enteredIntoNewContainer){
				ha = SuggestHorizontalAlignment(info.Bounds, availableSpaceRect);
				va = SuggestVerticalAlignment(info.Bounds, availableSpaceRect);
			}
			info.Item.Properties[Control.HorizontalAlignmentProperty].SetValue(ha);
			info.Item.Properties[Control.VerticalAlignmentProperty].SetValue(va);
			
			Thickness margin = new Thickness(0, 0, 0, 0);
			double mLeft = 0, mTop = 0, mRight = 0, mBottom = 0;
			if (ha == HorizontalAlignment.Left || ha == HorizontalAlignment.Stretch)
				mLeft = info.Bounds.Left - GetColumnOffset(leftColumnIndex);
			if (va == VerticalAlignment.Top || va == VerticalAlignment.Stretch)
				mTop = info.Bounds.Top - GetRowOffset(topRowIndex);
			if (ha == HorizontalAlignment.Right || ha == HorizontalAlignment.Stretch)
				mRight = GetColumnOffset(rightColumnIndex + 1) - info.Bounds.Right;
			if (va == VerticalAlignment.Bottom || va == VerticalAlignment.Stretch)
				mBottom = GetRowOffset(bottomRowIndex + 1) - info.Bounds.Bottom;
			info.Item.Properties[Control.MarginProperty].SetValue(new Thickness(mLeft, mTop, mRight, mBottom));
			
			if (ha == HorizontalAlignment.Stretch)
				info.Item.Properties[Control.WidthProperty].Reset();
            //else
            //    info.Item.Properties[Control.WidthProperty].SetValue(info.Bounds.Width);
			
			if (va == VerticalAlignment.Stretch)
				info.Item.Properties[Control.HeightProperty].Reset();
            //else
            //    info.Item.Properties[Control.HeightProperty].SetValue(info.Bounds.Height);
		}
		
		public override void LeaveContainer(PlacementOperation operation)
		{
			GrayOutDesignerExceptActiveArea.Stop(ref grayOut);
			base.LeaveContainer(operation);
			foreach (PlacementInformation info in operation.PlacedItems) {
				if (info.Item.ComponentType == typeof(ColumnDefinition)) {
					// TODO: combine the width of the deleted column with the previous column
					this.ExtendedItem.Properties["ColumnDefinitions"].CollectionElements.Remove(info.Item);
				} else if (info.Item.ComponentType == typeof(RowDefinition)) {
					this.ExtendedItem.Properties["RowDefinitions"].CollectionElements.Remove(info.Item);
				} else {
					info.Item.Properties.GetAttachedProperty(Grid.RowProperty).Reset();
					info.Item.Properties.GetAttachedProperty(Grid.ColumnProperty).Reset();
					info.Item.Properties.GetAttachedProperty(Grid.RowSpanProperty).Reset();
					info.Item.Properties.GetAttachedProperty(Grid.ColumnSpanProperty).Reset();

					HorizontalAlignment ha = info.Item.Properties[Control.HorizontalAlignmentProperty].GetConvertedValueOnInstance<HorizontalAlignment>();
					VerticalAlignment va = info.Item.Properties[Control.VerticalAlignmentProperty].GetConvertedValueOnInstance<VerticalAlignment>();

					if (ha == HorizontalAlignment.Stretch)
						info.Item.Properties[Control.WidthProperty].SetValue(info.Bounds.Width);
					if (va == VerticalAlignment.Stretch)
						info.Item.Properties[Control.HeightProperty].SetValue(info.Bounds.Height);
				}
			}
		}
	}
}
