 
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;
using Avalonia;

namespace Scadix.AxamlDesigner.Extensions
{
	[ExtensionFor(typeof(Control))]
	[ExtensionServer(typeof(PrimarySelectionExtensionServer))]
	public class MarginHandleExtension : AdornerProvider
	{
		private MarginHandle []_handles;
		private MarginHandle _leftHandle, _topHandle, _rightHandle, _bottomHandle;
		private Grid _grid;
		
		protected override void OnInitialized()
		{
			base.OnInitialized();
			if (this.ExtendedItem.Parent != null)
			{
				if (this.ExtendedItem.Parent.ComponentType == typeof(Grid)){
					Control extendedControl = (Control)this.ExtendedItem.Component;
					AdornerPanel adornerPanel = new AdornerPanel();
					
					// If the Element is rotated/skewed in the grid, then margin handles do not appear
					if (extendedControl.RenderTransform == null || extendedControl.RenderTransform.Value == Matrix.Identity)
					{
						_grid = this.ExtendedItem.Parent.View as Grid;
						_handles = new[]
						{
							_leftHandle = new MarginHandle(ExtendedItem, adornerPanel, HandleOrientation.Left),
							_topHandle = new MarginHandle(ExtendedItem, adornerPanel, HandleOrientation.Top),
							_rightHandle = new MarginHandle(ExtendedItem, adornerPanel, HandleOrientation.Right),
							_bottomHandle = new MarginHandle(ExtendedItem, adornerPanel, HandleOrientation.Bottom),
						};
						foreach(var handle in _handles) {
							handle.PointerPressed += OnMouseDown;
							handle.Stub.PointerPressed += OnMouseDown;
						}
						
						
					}
					
					if (adornerPanel != null)
						this.Adorners.Add(adornerPanel);
				}
			}
		}
		
		#region Change margin through handle/stub
		private void OnMouseDown(object sender, PointerPressedEventArgs e)
		{
			e.Handled = true;
			var row = this.ExtendedItem.Properties.GetAttachedProperty(Grid.RowProperty).GetConvertedValueOnInstance<int>();
			var rowSpan = this.ExtendedItem.Properties.GetAttachedProperty(Grid.RowSpanProperty).GetConvertedValueOnInstance<int>();

			var column = this.ExtendedItem.Properties.GetAttachedProperty(Grid.ColumnProperty).GetConvertedValueOnInstance<int>();
			var columnSpan = this.ExtendedItem.Properties.GetAttachedProperty(Grid.ColumnSpanProperty).GetConvertedValueOnInstance<int>();

			var margin = this.ExtendedItem.Properties[Control.MarginProperty].GetConvertedValueOnInstance<Thickness>();
			double mLeft = margin.Left, mTop = margin.Top, mRight = margin.Right, mBottom = margin.Bottom;

			var point = this.ExtendedItem.View.TranslatePoint(new Point(), _grid);
			var position = new Rect(point ?? new Point(), PlacementOperation.GetRealElementSize(this.ExtendedItem.View));
			MarginHandle handle = null;
			if (sender is MarginHandle)
				handle = sender as MarginHandle;
			if (sender is MarginStub)
				handle = ((MarginStub) sender).Handle;
			if (handle != null) {
				switch (handle.Orientation) {
					case HandleOrientation.Left:
						if (_rightHandle.IsVisible == true) {
							if (_leftHandle.IsVisible == true) {
								mLeft = 0;
								this.ExtendedItem.Properties[Control.WidthProperty].SetValue(position.Width);
								this.ExtendedItem.Properties[Control.HorizontalAlignmentProperty].SetValue(HorizontalAlignment.Right);
							} else {
								mLeft = position.Left - GetColumnOffset(column);
								this.ExtendedItem.Properties[Control.HorizontalAlignmentProperty].Reset();
								this.ExtendedItem.Properties[Control.WidthProperty].Reset();
							}
						} else {
							if (_leftHandle.IsVisible == true) {
								mLeft = 0;
								mRight = GetColumnOffset(column + columnSpan) - position.Right;
								this.ExtendedItem.Properties[Control.WidthProperty].SetValue(position.Width);
								this.ExtendedItem.Properties[Control.HorizontalAlignmentProperty].SetValue(HorizontalAlignment.Right);
							} else {
								mLeft = position.Left - GetColumnOffset(column);
								this.ExtendedItem.Properties[Control.HorizontalAlignmentProperty].SetValue(HorizontalAlignment.Left);
							}
						}
						break;
					case HandleOrientation.Top:
						if (_bottomHandle.IsVisible == true) {
							if (_topHandle.IsVisible == true) {
								mTop = 0;
								this.ExtendedItem.Properties[Control.HeightProperty].SetValue(position.Height);
								this.ExtendedItem.Properties[Control.VerticalAlignmentProperty].SetValue(VerticalAlignment.Bottom);
							} else {
								mTop = position.Top - GetRowOffset(row);
								this.ExtendedItem.Properties[Control.VerticalAlignmentProperty].Reset();
								this.ExtendedItem.Properties[Control.HeightProperty].Reset();
							}
						} else {
							if (_topHandle.IsVisible == true) {
								mTop = 0;
								mBottom = GetRowOffset(row + rowSpan) - position.Bottom;
								this.ExtendedItem.Properties[Control.HeightProperty].SetValue(position.Height);
								this.ExtendedItem.Properties[Control.VerticalAlignmentProperty].SetValue(VerticalAlignment.Bottom);
							} else {
								mTop = position.Top - GetRowOffset(row);
								this.ExtendedItem.Properties[Control.VerticalAlignmentProperty].SetValue(VerticalAlignment.Top);
							}
						}
						break;
					case HandleOrientation.Right:
						if (_leftHandle.IsVisible == true) {
							if (_rightHandle.IsVisible == true) {
								mRight = 0;
								this.ExtendedItem.Properties[Control.WidthProperty].SetValue(position.Width);
								this.ExtendedItem.Properties[Control.HorizontalAlignmentProperty].SetValue(HorizontalAlignment.Left);
							} else {
								mRight = GetColumnOffset(column + columnSpan) - position.Right;
								this.ExtendedItem.Properties[Control.HorizontalAlignmentProperty].Reset();
								this.ExtendedItem.Properties[Control.WidthProperty].Reset();
							}
						} else {
							if (_rightHandle.IsVisible == true) {
								mRight = 0;
								mLeft = position.Left - GetColumnOffset(column);
								this.ExtendedItem.Properties[Control.WidthProperty].SetValue(position.Width);
								this.ExtendedItem.Properties[Control.HorizontalAlignmentProperty].SetValue(HorizontalAlignment.Left);
							} else {
								mRight = GetColumnOffset(column + columnSpan) - position.Right;
								this.ExtendedItem.Properties[Control.HorizontalAlignmentProperty].SetValue(HorizontalAlignment.Right);
							}
						}
						break;
					case HandleOrientation.Bottom:
						if (_topHandle.IsVisible == true) {
							if (_bottomHandle.IsVisible == true) {
								mBottom = 0;
								this.ExtendedItem.Properties[Control.HeightProperty].SetValue(position.Height);
								this.ExtendedItem.Properties[Control.VerticalAlignmentProperty].SetValue(VerticalAlignment.Top);
							} else {
								mBottom = GetRowOffset(row + rowSpan) - position.Bottom;
								this.ExtendedItem.Properties[Control.VerticalAlignmentProperty].Reset();
								this.ExtendedItem.Properties[Control.HeightProperty].Reset();
							}
						} else {
							if (_bottomHandle.IsVisible == true) {
								mBottom = 0;
								mTop = position.Top - GetRowOffset(row);
								this.ExtendedItem.Properties[Control.HeightProperty].SetValue(position.Height);
								this.ExtendedItem.Properties[Control.VerticalAlignmentProperty].SetValue(VerticalAlignment.Top);
							} else {
								mBottom = GetRowOffset(row + rowSpan) - position.Bottom;
								this.ExtendedItem.Properties[Control.VerticalAlignmentProperty].SetValue(VerticalAlignment.Bottom);
							}
						}
						break;
				}
			}
			this.ExtendedItem.Properties[Control.MarginProperty].SetValue(new Thickness(mLeft, mTop, mRight, mBottom));
		}
		
		private double GetColumnOffset(int index)
		{
			if (_grid != null) {
				if (index == 0) return 0;
				double offset = 0;
				for (int i = 0; i < index && i < _grid.ColumnDefinitions.Count; i++)
					offset += _grid.ColumnDefinitions[i].ActualWidth;
				return offset;
			}
			return 0;
		}

		private double GetRowOffset(int index)
		{
			if (_grid != null) {
				if (index == 0) return 0;
				double offset = 0;
				for (int i = 0; i < index && i < _grid.RowDefinitions.Count; i++)
					offset += _grid.RowDefinitions[i].ActualHeight;
				return offset;
			}
			return 0;
		}
		
		#endregion
		
		public void HideHandles()
		{
			if (_handles != null) {
				foreach (var handle in _handles) {
					handle.ShouldBeVisible = false;
					handle.IsVisible = false;
				}
			}
		}
		
		public void ShowHandles()
		{
			if(_handles!=null) {
				foreach(var handle in _handles) {
					handle.ShouldBeVisible = true;
					handle.IsVisible = true;
					handle.DecideVisiblity(handle.HandleLength);
				}
			}
		}
	}
}

