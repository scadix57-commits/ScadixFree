// Copyright (c) 2019 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Diagnostics;
using System.Globalization;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;

namespace Scadix.AxamlDesigner.Controls
{
	// Helper to get column/row offsets in Avalonia (WPF has .Offset property, Avalonia does not)
	internal static class GridDefinitionHelper
	{
		public static double GetColumnOffset(Grid grid, ColumnDefinition col)
		{
			double offset = 0;
			foreach (var c in grid.ColumnDefinitions) {
				if (c == col) break;
				offset += c.ActualWidth;
			}
			return offset;
		}
		public static double GetRowOffset(Grid grid, RowDefinition row)
		{
			double offset = 0;
			foreach (var r in grid.RowDefinitions) {
				if (r == row) break;
				offset += r.ActualHeight;
			}
			return offset;
		}
	}

	/// <summary>
	/// Adorner that displays the blue bar next to grids that can be used to create new rows/column.
	/// </summary>
	public class GridRailAdorner : Control
	{
		static GridRailAdorner()
		{
			bgBrush = new SolidColorBrush(Color.FromArgb(0x35, 0x1E, 0x90, 0xff));
		}
		
		readonly DesignItem gridItem;
		readonly Grid grid;
		readonly AdornerPanel adornerPanel;
		readonly GridSplitterAdorner previewAdorner;
		readonly Orientation orientation;
		readonly GridUnitSelector unitSelector;
		
		static readonly SolidColorBrush bgBrush;
		
		public const double RailSize = 10;
		public const double RailDistance = 6;
		public const double SplitterWidth = 10;

		bool displayUnitSelector; // Indicates whether Grid UnitSeletor should be displayed.
		
		public GridRailAdorner(DesignItem gridItem, AdornerPanel adornerPanel, Orientation orientation)
		{
			Debug.Assert(gridItem != null);
			Debug.Assert(adornerPanel != null);
			
			this.gridItem = gridItem;
			this.grid = (Grid)gridItem.Component;
			this.adornerPanel = adornerPanel;
			this.orientation = orientation;
			this.displayUnitSelector = false;
			this.unitSelector = new GridUnitSelector(this);
			adornerPanel.Children.Add(unitSelector);
			
			if (orientation == Orientation.Horizontal) {
				this.Height = RailSize;
				previewAdorner = new GridColumnSplitterAdorner(this, gridItem, null, null);
			} else { // vertical
				this.Width = RailSize;
				previewAdorner = new GridRowSplitterAdorner(this, gridItem, null, null);
			}
			unitSelector.Orientation = orientation;
			previewAdorner.IsPreview = true;
			previewAdorner.IsHitTestVisible = false;
			unitSelector.IsVisible = false;
		}
		
		public override void Render(DrawingContext drawingContext)
		{
			base.Render(drawingContext);
			
			if (orientation == Orientation.Horizontal) {
				Rect bgRect = new Rect(0, 0, grid.Bounds.Width, RailSize);
				drawingContext.DrawRectangle(bgBrush, null, bgRect);
				
				DesignItemProperty colCollection = gridItem.Properties["ColumnDefinitions"];
				foreach (var colItem in colCollection.CollectionElements) {
					ColumnDefinition column = colItem.Component as ColumnDefinition;
					if (column.ActualWidth < 0) continue;
					GridLength len = colItem.Properties[ColumnDefinition.WidthProperty].GetConvertedValueOnInstance<GridLength>();
					double colOffset = GridDefinitionHelper.GetColumnOffset(grid, column);
					FormattedText text = new FormattedText(GridLengthToText(len), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brushes.Black);
					text.TextAlignment = TextAlignment.Center;
					drawingContext.DrawText(text, new Point(colOffset + column.ActualWidth / 2, 0));
				}
			} else {
				Rect bgRect = new Rect(0, 0, RailSize, grid.Bounds.Height);
				drawingContext.DrawRectangle(bgBrush, null, bgRect);
				
				DesignItemProperty rowCollection = gridItem.Properties["RowDefinitions"];
				foreach (var rowItem in rowCollection.CollectionElements) {
					RowDefinition row = rowItem.Component as RowDefinition;
					if (row.ActualHeight < 0) continue;
					GridLength len = rowItem.Properties[RowDefinition.HeightProperty].GetConvertedValueOnInstance<GridLength>();
					double rowOffset = GridDefinitionHelper.GetRowOffset(grid, row);
					FormattedText text = new FormattedText(GridLengthToText(len), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brushes.Black);
					text.TextAlignment = TextAlignment.Center;
					using (drawingContext.PushTransform(Matrix.CreateRotation(-Math.PI / 2))) {
						drawingContext.DrawText(text, new Point((rowOffset + row.ActualHeight / 2) * -1, 0));
					}
				}
			}
		}
		
		#region Handle mouse events to add a new row/column
		protected override void OnPointerEntered(PointerEventArgs e)
		{
			base.OnPointerEntered(e);
			this.Cursor = new Cursor(StandardCursorType.Cross);
			RelativePlacement rpUnitSelector = new RelativePlacement();
			if (orientation == Orientation.Vertical)
			{
				double insertionPosition = e.GetPosition(grid).Y;
				RowDefinition current = grid.RowDefinitions
					.FirstOrDefault(r => {
						double off = GridDefinitionHelper.GetRowOffset(grid, r);
						return insertionPosition >= off && insertionPosition <= (off + r.ActualHeight);
					});
				if (current != null)
				{
					double rowOffset = GridDefinitionHelper.GetRowOffset(grid, current);
					DesignItem component = this.gridItem.Services.Component.GetDesignItem(current);
					if (component == null) { displayUnitSelector = false; goto done; }
					rpUnitSelector.XOffset = -(RailSize + RailDistance) * 2.75 - 6;
					rpUnitSelector.WidthOffset = RailSize + RailDistance;
					rpUnitSelector.WidthRelativeToContentWidth = 1;
					rpUnitSelector.HeightOffset = 55;
					rpUnitSelector.YOffset = rowOffset + current.ActualHeight / 2 - 25;
					unitSelector.SelectedItem = component;
					unitSelector.Unit = component.Properties[RowDefinition.HeightProperty].GetConvertedValueOnInstance<GridLength>().GridUnitType;
					displayUnitSelector = true;
				}
				else
				{
					displayUnitSelector = false;
				}
			}
			else
			{
				double insertionPosition = e.GetPosition(grid).X;
				ColumnDefinition current = grid.ColumnDefinitions
					.FirstOrDefault(r => {
						double off = GridDefinitionHelper.GetColumnOffset(grid, r);
						return insertionPosition >= off && insertionPosition <= (off + r.ActualWidth);
					});
				if (current != null)
				{
					double colOffset = GridDefinitionHelper.GetColumnOffset(grid, current);
					DesignItem component = this.gridItem.Services.Component.GetDesignItem(current);
					if (component == null) { displayUnitSelector = false; goto done; }
					Debug.Assert(component != null);
					rpUnitSelector.YOffset = -(RailSize + RailDistance) * 2.20 - 6;
					rpUnitSelector.HeightOffset = RailSize + RailDistance;
					rpUnitSelector.HeightRelativeToContentHeight = 1;
					rpUnitSelector.WidthOffset = 75;
					rpUnitSelector.XOffset = colOffset + current.ActualWidth / 2 - 35;
					unitSelector.SelectedItem = component;
					unitSelector.Unit = component.Properties[ColumnDefinition.WidthProperty].GetConvertedValueOnInstance<GridLength>().GridUnitType;
					displayUnitSelector = true;
				}
				else
				{
					displayUnitSelector = false;
				}
			}
			done:
			if(displayUnitSelector)
				unitSelector.IsVisible = true;
			if(!adornerPanel.Children.Contains(previewAdorner))
				adornerPanel.Children.Add(previewAdorner);
		}
		
		protected override void OnPointerMoved(PointerEventArgs e)
		{
			base.OnPointerMoved(e);
			RelativePlacement rp = new RelativePlacement();
			RelativePlacement rpUnitSelector = new RelativePlacement();
			if (orientation == Orientation.Vertical)
			{
				double insertionPosition = e.GetPosition(grid).Y;
				RowDefinition current = grid.RowDefinitions
					.FirstOrDefault(r => {
						double off = GridDefinitionHelper.GetRowOffset(grid, r);
						return insertionPosition >= off && insertionPosition <= (off + r.ActualHeight);
					});
				rp.XOffset = -(RailSize + RailDistance);
				rp.WidthOffset = RailSize + RailDistance;
				rp.WidthRelativeToContentWidth = 1;
				rp.HeightOffset = SplitterWidth;
				rp.YOffset = e.GetPosition(grid).Y - SplitterWidth / 2;
				if (current != null)
				{
					double rowOffset = GridDefinitionHelper.GetRowOffset(grid, current);
					DesignItem component = this.gridItem.Services.Component.GetDesignItem(current);
					if (component == null) { displayUnitSelector = false; }
					else {
					rpUnitSelector.XOffset = -(RailSize + RailDistance) * 2.75 - 6;
					rpUnitSelector.WidthOffset = RailSize + RailDistance;
					rpUnitSelector.WidthRelativeToContentWidth = 1;
					rpUnitSelector.HeightOffset = 55;
					rpUnitSelector.YOffset = rowOffset + current.ActualHeight / 2 - 25;
					unitSelector.SelectedItem = component;
					unitSelector.Unit = component.Properties[RowDefinition.HeightProperty].GetConvertedValueOnInstance<GridLength>().GridUnitType;
					displayUnitSelector = true;
					}
				}
				else
				{
					displayUnitSelector = false;
				}
			}
			else
			{
				double insertionPosition = e.GetPosition(grid).X;
				ColumnDefinition current = grid.ColumnDefinitions
					.FirstOrDefault(r => {
						double off = GridDefinitionHelper.GetColumnOffset(grid, r);
						return insertionPosition >= off && insertionPosition <= (off + r.ActualWidth);
					});
				rp.YOffset = -(RailSize + RailDistance);
				rp.HeightOffset = RailSize + RailDistance;
				rp.HeightRelativeToContentHeight = 1;
				rp.WidthOffset = SplitterWidth;
				rp.XOffset = e.GetPosition(grid).X - SplitterWidth / 2;
				if (current != null)
				{
					double colOffset = GridDefinitionHelper.GetColumnOffset(grid, current);
					DesignItem component = this.gridItem.Services.Component.GetDesignItem(current);
					if (component == null) { displayUnitSelector = false; }
					else {
					Debug.Assert(component != null);
					rpUnitSelector.YOffset = -(RailSize + RailDistance) * 2.20 - 6;
					rpUnitSelector.HeightOffset = RailSize + RailDistance;
					rpUnitSelector.HeightRelativeToContentHeight = 1;
					rpUnitSelector.WidthOffset = 75;
					rpUnitSelector.XOffset = colOffset + current.ActualWidth / 2 - 35;
					unitSelector.SelectedItem = component;
					unitSelector.Unit = component.Properties[ColumnDefinition.WidthProperty].GetConvertedValueOnInstance<GridLength>().GridUnitType;
					displayUnitSelector = true;
					}
				}
				else
				{
					displayUnitSelector = false;
				}
			}
			AdornerPanel.SetPlacement(previewAdorner, rp);
			if (displayUnitSelector)
				AdornerPanel.SetPlacement(unitSelector, rpUnitSelector);
		}
		
		protected override void OnPointerExited(PointerEventArgs e)
		{
			base.OnPointerExited(e);
			if (!unitSelector.IsPointerOver)
			{
				unitSelector.IsVisible = false;
				displayUnitSelector = false;
			}
			if(adornerPanel.Children.Contains(previewAdorner))
				adornerPanel.Children.Remove(previewAdorner);
		}
		
		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			e.Handled = true;
			Focus();
			adornerPanel.Children.Remove(previewAdorner);
			if (orientation == Orientation.Vertical) {
				double insertionPosition = e.GetPosition(grid).Y;
				DesignItemProperty rowCollection = gridItem.Properties["RowDefinitions"];
				DesignItem currentRow = null;
				using (ChangeGroup changeGroup = gridItem.OpenGroup("Split grid row")) {
					if (rowCollection.CollectionElements.Count == 0) {
						DesignItem firstRow = gridItem.Services.Component.RegisterComponentForDesigner(new RowDefinition());
						rowCollection.CollectionElements.Add(firstRow);
						grid.UpdateLayout();
						currentRow = firstRow;
					} else {
						RowDefinition current = grid.RowDefinitions
							.FirstOrDefault(r => {
								double off = GridDefinitionHelper.GetRowOffset(grid, r);
								return insertionPosition >= off && insertionPosition <= (off + r.ActualHeight);
							});
						if (current != null)
							currentRow = gridItem.Services.Component.GetDesignItem(current);
					}
					if (currentRow == null)
						currentRow = gridItem.Services.Component.GetDesignItem(grid.RowDefinitions.Last());
					unitSelector.SelectedItem = currentRow;
					for (int i = 0; i < grid.RowDefinitions.Count; i++) {
						RowDefinition row = grid.RowDefinitions[i];
						double rowOffset = GridDefinitionHelper.GetRowOffset(grid, row);
						if (rowOffset > insertionPosition) continue;
						if (rowOffset + row.ActualHeight < insertionPosition) continue;
						// split row
						GridLength oldLength = gridItem.Services.Component.GetDesignItem(row).Properties[RowDefinition.HeightProperty].GetConvertedValueOnInstance<GridLength>();
						GridLength newLength1, newLength2;
						SplitLength(oldLength, insertionPosition - rowOffset, row.ActualHeight, out newLength1, out newLength2);
						DesignItem newRowDefinition = gridItem.Services.Component.RegisterComponentForDesigner(new RowDefinition());
						rowCollection.CollectionElements.Insert(i + 1, newRowDefinition);
						rowCollection.CollectionElements[i].Properties[RowDefinition.HeightProperty].SetValue(newLength1);
						newRowDefinition.Properties[RowDefinition.HeightProperty].SetValue(newLength2);
						grid.UpdateLayout();
						FixIndicesAfterSplit(i, Grid.RowProperty, Grid.RowSpanProperty, insertionPosition);
						grid.UpdateLayout();
						changeGroup.Commit();
						break;
					}
				}
			} else {
				double insertionPosition = e.GetPosition(grid).X;
				DesignItemProperty columnCollection = gridItem.Properties["ColumnDefinitions"];
				DesignItem currentColumn = null;
				using (ChangeGroup changeGroup = gridItem.OpenGroup("Split grid column")) {
					if (columnCollection.CollectionElements.Count == 0) {
						DesignItem firstColumn = gridItem.Services.Component.RegisterComponentForDesigner(new ColumnDefinition());
						columnCollection.CollectionElements.Add(firstColumn);
						grid.UpdateLayout();
						currentColumn = firstColumn;
					} else {
						ColumnDefinition current = grid.ColumnDefinitions
							.FirstOrDefault(r => {
								double off = GridDefinitionHelper.GetColumnOffset(grid, r);
								return insertionPosition >= off && insertionPosition <= (off + r.ActualWidth);
							});
						if (current != null)
							currentColumn = gridItem.Services.Component.GetDesignItem(current);
					}
					if (currentColumn == null)
						currentColumn = gridItem.Services.Component.GetDesignItem(grid.ColumnDefinitions.Last());
					unitSelector.SelectedItem = currentColumn;
					for (int i = 0; i < grid.ColumnDefinitions.Count; i++) {
						ColumnDefinition column = grid.ColumnDefinitions[i];
						double colOffset = GridDefinitionHelper.GetColumnOffset(grid, column);
						if (colOffset > insertionPosition) continue;
						if (colOffset + column.ActualWidth < insertionPosition) continue;
						// split column
						GridLength oldLength = gridItem.Services.Component.GetDesignItem(column).Properties[ColumnDefinition.WidthProperty].GetConvertedValueOnInstance<GridLength>();
						GridLength newLength1, newLength2;
						SplitLength(oldLength, insertionPosition - colOffset, column.ActualWidth, out newLength1, out newLength2);
						DesignItem newColumnDefinition = gridItem.Services.Component.RegisterComponentForDesigner(new ColumnDefinition());
						columnCollection.CollectionElements.Insert(i + 1, newColumnDefinition);
						columnCollection.CollectionElements[i].Properties[ColumnDefinition.WidthProperty].SetValue(newLength1);
						newColumnDefinition.Properties[ColumnDefinition.WidthProperty].SetValue(newLength2);
						grid.UpdateLayout();
						FixIndicesAfterSplit(i, Grid.ColumnProperty, Grid.ColumnSpanProperty, insertionPosition);
						changeGroup.Commit();
						grid.UpdateLayout();
						break;
					}
				}
			}
			InvalidateVisual();
		}
		
		private void FixIndicesAfterSplit(int splitIndex, AvaloniaProperty idxProperty, AvaloniaProperty spanProperty, double insertionPostion)
		{
			if (orientation == Orientation.Horizontal) {
				foreach (DesignItem child in gridItem.Properties["Children"].CollectionElements) {
					var childView = child.View as Visual;
					Point? topLeftNullable = childView?.TranslatePoint(new Point(0, 0), grid);
					if (!topLeftNullable.HasValue) continue;
					Point topLeft = topLeftNullable.Value;
					var margin = child.Properties[Control.MarginProperty].GetConvertedValueOnInstance<Thickness>();
					var start = child.Properties.GetAttachedProperty(idxProperty).GetConvertedValueOnInstance<int>();
					var span = child.Properties.GetAttachedProperty(spanProperty).GetConvertedValueOnInstance<int>();
					if (start <= splitIndex && splitIndex < start + span) {
						var width = child.Properties[Control.WidthProperty].GetConvertedValueOnInstance<double>();
						if (double.IsNaN(width)) width = ((Control)child.Component).Bounds.Width;						if (insertionPostion >= topLeft.X + width) {
							continue;
						}
						if (insertionPostion > topLeft.X)
							child.Properties.GetAttachedProperty(spanProperty).SetValue(span + 1);
						else {
							child.Properties.GetAttachedProperty(idxProperty).SetValue(start + 1);
							child.Properties[Control.MarginProperty].SetValue(new Thickness(topLeft.X - insertionPostion, margin.Top, margin.Right, margin.Bottom));
						}
					}
					else if (start > splitIndex)
					{
						child.Properties.GetAttachedProperty(idxProperty).SetValue(start + 1);
					}
				}
			}
			else
			{
				foreach (DesignItem child in gridItem.Properties["Children"].CollectionElements)
				{
					var childView = child.View as Visual;
					Point? topLeftNullable = childView?.TranslatePoint(new Point(0, 0), grid);
					if (!topLeftNullable.HasValue) continue;
					Point topLeft = topLeftNullable.Value;
					var margin = child.Properties[Control.MarginProperty].GetConvertedValueOnInstance<Thickness>();
					var start = child.Properties.GetAttachedProperty(idxProperty).GetConvertedValueOnInstance<int>();
					var span = child.Properties.GetAttachedProperty(spanProperty).GetConvertedValueOnInstance<int>();
					if (start <= splitIndex && splitIndex < start + span)
					{
						var height = child.Properties[Control.HeightProperty].GetConvertedValueOnInstance<double>();
						if (double.IsNaN(height)) height = ((Control)child.Component).Bounds.Height;
						if (insertionPostion >= topLeft.Y + height)
							continue;
						if (insertionPostion > topLeft.Y)
							child.Properties.GetAttachedProperty(spanProperty).SetValue(span + 1);
						else {
							child.Properties.GetAttachedProperty(idxProperty).SetValue(start + 1);
							child.Properties[Control.MarginProperty].SetValue(new Thickness(margin.Left, topLeft.Y - insertionPostion, margin.Right, margin.Bottom));
						}
					}
					else if (start > splitIndex)
					{
						child.Properties.GetAttachedProperty(idxProperty).SetValue(start + 1);
					}
				}
			}
		}
		
		static void SplitLength(GridLength oldLength, double insertionPosition, double oldActualValue,
		                        out GridLength newLength1, out GridLength newLength2)
		{
			if (oldLength.IsAuto) {
				oldLength = new GridLength(oldActualValue);
			}
			double percentage = insertionPosition / oldActualValue;
			newLength1 = new GridLength(oldLength.Value * percentage, oldLength.GridUnitType);
			newLength2 = new GridLength(oldLength.Value - newLength1.Value, oldLength.GridUnitType);
		}
		#endregion
		
		string GridLengthToText(GridLength len)
		{
			switch (len.GridUnitType) {
				case GridUnitType.Auto:
					return "Auto";
				case GridUnitType.Star:
					return len.Value == 1 ? "*" : Math.Round(len.Value, 2) + "*";
				case GridUnitType.Pixel:
					return Math.Round(len.Value, 2) + "px";
			}
			return string.Empty;
		}
		
		public void SetGridLengthUnit(GridUnitType unit)
		{
			DesignItem item = unitSelector.SelectedItem;
			grid.UpdateLayout();
			
			Debug.Assert(item != null);
			
			if (orientation == Orientation.Vertical) {
				SetGridLengthUnit(unit, item, RowDefinition.HeightProperty);
			} else {
				SetGridLengthUnit(unit, item, ColumnDefinition.WidthProperty);
			}
			grid.UpdateLayout();
			InvalidateVisual();
		}
		
		void SetGridLengthUnit(GridUnitType unit, DesignItem item, AvaloniaProperty property)
		{
			DesignItemProperty itemProperty = item.Properties[property];
			GridLength oldValue = itemProperty.GetConvertedValueOnInstance<GridLength>();
			GridLength value = GetNewGridLength(unit, oldValue);
			
			if (value != oldValue) {
				itemProperty.SetValue(value);
			}
		}
		
		GridLength GetNewGridLength(GridUnitType unit, GridLength oldValue)
		{
			if (unit == GridUnitType.Auto) {
				return GridLength.Auto;
			}
			return new GridLength(oldValue.Value, unit);
		}
	}
	
	public abstract class GridSplitterAdorner : TemplatedControl
	{
		public static readonly StyledProperty<bool> IsPreviewProperty
			= AvaloniaProperty.Register<GridSplitterAdorner, bool>("IsPreview", false);
		
		protected readonly Grid grid;
		protected readonly DesignItem gridItem;
		protected readonly DesignItem firstRow, secondRow; // can also be columns
		protected readonly GridRailAdorner rail;
		
		internal GridSplitterAdorner(GridRailAdorner rail, DesignItem gridItem, DesignItem firstRow, DesignItem secondRow)
		{
			Debug.Assert(gridItem != null);
			this.grid = (Grid)gridItem.Component;
			this.gridItem = gridItem;
			this.firstRow = firstRow;
			this.secondRow = secondRow;
			this.rail = rail;
		}
		
		public bool IsPreview {
			get { return GetValue(IsPreviewProperty); }
			set { SetValue(IsPreviewProperty, value); }
		}
		
		ChangeGroup activeChangeGroup;
		double mouseStartPos;
		bool mouseIsDown;
		
		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			e.Handled = true;
			e.Pointer.Capture(this);
			Focus();
			mouseStartPos = GetCoordinate(e.GetPosition(grid));
			mouseIsDown = true;
		}
		
		protected override void OnPointerMoved(PointerEventArgs e)
		{
			if (mouseIsDown) {
				double mousePos = GetCoordinate(e.GetPosition(grid));
				if (activeChangeGroup == null) {
					if (Math.Abs(mousePos - mouseStartPos) >= 4.0) {
						activeChangeGroup = gridItem.OpenGroup("Change grid row/column size");
						RememberOriginalSize();
					}
				}
				if (activeChangeGroup != null) {
					ChangeSize(mousePos - mouseStartPos);
				}
			}
		}
		
		protected GridLength original1, original2;
		protected double originalPixelSize1, originalPixelSize2;
		
		protected abstract double GetCoordinate(Point point);
		protected abstract void RememberOriginalSize();
		protected abstract AvaloniaProperty RowColumnSizeProperty { get; }
		
		void ChangeSize(double delta)
		{
			if (delta < -originalPixelSize1) delta = -originalPixelSize1;
			if (delta > originalPixelSize2) delta = originalPixelSize2;
			if (original1.IsAuto) original1 = new GridLength(originalPixelSize1);
			if (original2.IsAuto) original2 = new GridLength(originalPixelSize2);
			GridLength new1;
			if (original1.IsStar && originalPixelSize1 > 0)
				new1 = new GridLength(original1.Value * (originalPixelSize1 + delta) / originalPixelSize1, GridUnitType.Star);
			else
				new1 = new GridLength(originalPixelSize1 + delta);
			GridLength new2;
			if (original2.IsStar && originalPixelSize2 > 0)
				new2 = new GridLength(original2.Value * (originalPixelSize2 - delta) / originalPixelSize2, GridUnitType.Star);
			else
				new2 = new GridLength(originalPixelSize2 - delta);
			firstRow.Properties[RowColumnSizeProperty].SetValue(new1);
			secondRow.Properties[RowColumnSizeProperty].SetValue(new2);
			var parent = this.GetVisualParent() as Control;
			parent?.InvalidateArrange();
			rail.InvalidateVisual();
		}
		
		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			if (activeChangeGroup != null) {
				activeChangeGroup.Commit();
				activeChangeGroup = null;
			}
			Stop();
		}
		
		protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
		{
			Stop();
		}
		
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape) {
				e.Handled = true;
				Stop();
			}
		}
		
		protected void Stop()
		{
			mouseIsDown = false;
			if (activeChangeGroup != null) {
				activeChangeGroup.Abort();
				activeChangeGroup = null;
			}
		}
	}
	
	public class GridRowSplitterAdorner : GridSplitterAdorner
	{
		protected override Type StyleKeyOverride => typeof(GridRowSplitterAdorner);

		static GridRowSplitterAdorner()
		{
			CursorProperty.OverrideDefaultValue<GridRowSplitterAdorner>(new Cursor(StandardCursorType.SizeNorthSouth));
		}
		
		internal GridRowSplitterAdorner(GridRailAdorner rail, DesignItem gridItem, DesignItem firstRow, DesignItem secondRow)
			: base(rail, gridItem, firstRow, secondRow)
		{
		}
		
		protected override double GetCoordinate(Point point)
		{
			return point.Y;
		}
		
		protected override void RememberOriginalSize()
		{
			RowDefinition r1 = (RowDefinition)firstRow.Component;
			RowDefinition r2 = (RowDefinition)secondRow.Component;
			original1 = firstRow.Properties[RowDefinition.HeightProperty].GetConvertedValueOnInstance<GridLength>();
			original2 = secondRow.Properties[RowDefinition.HeightProperty].GetConvertedValueOnInstance<GridLength>();
			originalPixelSize1 = r1.ActualHeight;
			originalPixelSize2 = r2.ActualHeight;
		}
		
		protected override AvaloniaProperty RowColumnSizeProperty {
			get { return RowDefinition.HeightProperty; }
		}
	}
	
	public sealed class GridColumnSplitterAdorner : GridSplitterAdorner
	{
		protected override Type StyleKeyOverride => typeof(GridColumnSplitterAdorner);

		static GridColumnSplitterAdorner()
		{
			CursorProperty.OverrideDefaultValue<GridColumnSplitterAdorner>(new Cursor(StandardCursorType.SizeWestEast));
		}
		
		internal GridColumnSplitterAdorner(GridRailAdorner rail, DesignItem gridItem, DesignItem firstRow, DesignItem secondRow)
			: base(rail, gridItem, firstRow, secondRow)
		{
		}
		
		protected override double GetCoordinate(Point point)
		{
			return point.X;
		}
		
		protected override void RememberOriginalSize()
		{
			ColumnDefinition r1 = (ColumnDefinition)firstRow.Component;
			ColumnDefinition r2 = (ColumnDefinition)secondRow.Component;
			original1 = firstRow.Properties[ColumnDefinition.WidthProperty].GetConvertedValueOnInstance<GridLength>();
			original2 = secondRow.Properties[ColumnDefinition.WidthProperty].GetConvertedValueOnInstance<GridLength>();
			originalPixelSize1 = r1.ActualWidth;
			originalPixelSize2 = r2.ActualWidth;
		}
		
		protected override AvaloniaProperty RowColumnSizeProperty {
			get { return ColumnDefinition.WidthProperty; }
		}
	}
}
