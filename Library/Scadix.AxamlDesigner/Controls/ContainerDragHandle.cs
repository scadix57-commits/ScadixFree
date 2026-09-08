
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Scadix.AxamlDesign.UIExtensions;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// A thumb where the look can depend on the IsPrimarySelection property.
	/// Used by ControlSelectionRectangle.
	/// </summary>
	public class ContainerDragHandle : TemplatedControl
	{
		protected override Type StyleKeyOverride => typeof(ContainerDragHandle);
		
		private ScaleTransform scaleTransform;

		public ContainerDragHandle()
		{
			scaleTransform = new ScaleTransform(1.0, 1.0);

            var lt = new Avalonia.Controls.LayoutTransformControl()
            {
                LayoutTransform = scaleTransform
            };
            
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			var surface = this.TryFindParent<DesignSurface>();
			if (surface != null && surface.ZoomControl != null)
			{
				var bnd = new Binding("CurrentZoom") { Source = surface.ZoomControl };
				bnd.Converter = InvertedZoomConverter.Instance;

				scaleTransform.Bind(ScaleTransform.ScaleXProperty, bnd);
				scaleTransform.Bind(ScaleTransform.ScaleYProperty, bnd);
			}
		}	}
}
