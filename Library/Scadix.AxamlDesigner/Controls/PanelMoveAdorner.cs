 

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.UIExtensions;
using Scadix.AxamlDesigner.Services;

namespace Scadix.AxamlDesigner.Controls
{
	public class PanelMoveAdorner : TemplatedControl
    {
		protected override Type StyleKeyOverride => typeof(PanelMoveAdorner);
		
		private ScaleTransform scaleTransform;

		public PanelMoveAdorner(DesignItem item)
		{
			this.item = item;
           
            scaleTransform = new ScaleTransform(1.0, 1.0);
            var lt = new Avalonia.Controls.LayoutTransformControl()
            {
                LayoutTransform = scaleTransform
            };
            
		}

		DesignItem item;

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			e.Handled = true;
			//item.Services.Selection.SetSelectedComponents(new DesignItem [] { item }, SelectionTypes.Auto);
			new DragMoveMouseGesture(item, false, true).Start(item.Services.DesignPanel, e);
		}
		
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			// Bind IsVisible to item.Component.IsVisible
			var bnd = new Binding("IsVisible") { Source = item.Component };
            bnd.Converter = CollapsedWhenFalse.Instance;
            this.Bind(Avalonia.Visual.IsVisibleProperty, bnd);

			var surface = this.TryFindParent<DesignSurface>();
			if (surface != null && surface.ZoomControl != null)
			{
				var zoomBnd = new Binding("CurrentZoom") { Source = surface.ZoomControl };
				zoomBnd.Converter = InvertedZoomConverter.Instance;

				scaleTransform.Bind(ScaleTransform.ScaleXProperty, zoomBnd);
				scaleTransform.Bind(ScaleTransform.ScaleYProperty, zoomBnd);
			}
		}
	}
}
