 

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;

namespace Scadix.AxamlDesigner.Extensions
{
	[ExtensionServer(typeof(OnlyOneItemSelectedExtensionServer))]
	[ExtensionFor(typeof(Control))]
	public class RenderTransformOriginExtension : SelectionAdornerProvider
	{
		readonly AdornerPanel adornerPanel;
		RenderTransformOriginThumb renderTransformOriginThumb;

		RelativePoint renderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

		public RenderTransformOriginExtension()
		{
			adornerPanel = new AdornerPanel();
			adornerPanel.Order = AdornerOrder.Foreground;
			this.Adorners.Add(adornerPanel);

			CreateRenderTransformOriginThumb();
		}

		void CreateRenderTransformOriginThumb()
		{
			renderTransformOriginThumb = new RenderTransformOriginThumb();
			renderTransformOriginThumb.Cursor = new Cursor(StandardCursorType.Hand);
			renderTransformOriginThumb.Width = 10;
			renderTransformOriginThumb.Height = 10;
			renderTransformOriginThumb.Opacity = 1;

			AdornerPanel.SetPlacement(renderTransformOriginThumb,
				new RelativePlacement(HorizontalAlignment.Left, VerticalAlignment.Top) {
					XRelativeToContentWidth  = renderTransformOrigin.Point.X,
					YRelativeToContentHeight = renderTransformOrigin.Point.Y
				});
			adornerPanel.Children.Add(renderTransformOriginThumb);

			DragListener drag = new DragListener(renderTransformOriginThumb);
			drag.Changed   += renderTransformOriginThumb_DragDelta;
			drag.Completed += renderTransformOriginThumb_DragCompleted;
		}

		void renderTransformOriginThumb_DragCompleted(DragListener drag)
		{
			var x = Math.Round(renderTransformOrigin.Point.X * 100, 1);
			var y = Math.Round(renderTransformOrigin.Point.Y * 100, 1);
			ExtendedItem.Properties.GetProperty(Visual.RenderTransformOriginProperty)
				.SetValue($"{x}%,{y}%");
		}

		void renderTransformOriginThumb_DragDelta(DragListener drag)
		{
			var p = AdornerPanel.GetPlacement(renderTransformOriginThumb) as RelativePlacement;
			if (p == null) return;

			// Use DeltaDelta (incremental per-frame delta) same as WPF e.HorizontalChange/e.VerticalChange
			var pointAbs    = adornerPanel.RelativeToAbsolute(new Vector(p.XRelativeToContentWidth, p.YRelativeToContentHeight));
			var pointAbsNew = pointAbs + new Vector(drag.DeltaDelta.X, drag.DeltaDelta.Y);
			var pRel        = adornerPanel.AbsoluteToRelative(pointAbsNew);

			renderTransformOrigin = new RelativePoint(pRel.X, pRel.Y, RelativeUnit.Relative);

			// Update placement so thumb moves visually
			AdornerPanel.SetPlacement(renderTransformOriginThumb,
				new RelativePlacement(HorizontalAlignment.Left, VerticalAlignment.Top) {
					XRelativeToContentWidth  = pRel.X,
					YRelativeToContentHeight = pRel.Y
				});
			adornerPanel.InvalidateMeasure();

			// Update the property on the view directly (same as WPF)
			this.ExtendedItem.View.SetValue(Visual.RenderTransformOriginProperty, renderTransformOrigin);
		}

		protected override void OnInitialized()
		{
			base.OnInitialized();
			this.ExtendedItem.PropertyChanged += OnPropertyChanged;

			if (this.ExtendedItem.Properties.GetProperty(Visual.RenderTransformOriginProperty).IsSet) {
				renderTransformOrigin = this.ExtendedItem.Properties.GetProperty(Visual.RenderTransformOriginProperty)
					.GetConvertedValueOnInstance<RelativePoint>();
			}

			AdornerPanel.SetPlacement(renderTransformOriginThumb,
				new RelativePlacement(HorizontalAlignment.Left, VerticalAlignment.Top) {
					XRelativeToContentWidth  = renderTransformOrigin.Point.X,
					YRelativeToContentHeight = renderTransformOrigin.Point.Y
				});
		}

		void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{ }

		protected override void OnRemove()
		{
			this.ExtendedItem.PropertyChanged -= OnPropertyChanged;
			base.OnRemove();
		}
	}
}
