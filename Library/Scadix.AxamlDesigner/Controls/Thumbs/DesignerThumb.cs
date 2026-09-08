
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.UIExtensions;

namespace Scadix.AxamlDesigner.Controls.Thumbs
{
	/// <summary>
	/// A thumb where the look can depend on the IsPrimarySelection property.
	/// </summary>
	public class DesignerThumb : Thumb
	{



        
        /// <summary>
        /// Dependency property for <see cref="IsPrimarySelection"/>.
        /// </summary>
        public static readonly StyledProperty<bool> IsPrimarySelectionProperty
			= AvaloniaProperty.Register<DesignerThumb, bool>("IsPrimarySelection");
		
		/// <summary>
		/// Dependency property for <see cref="IsPrimarySelection"/>.
		/// </summary>
		public static readonly StyledProperty<bool> ThumbVisibleProperty
			= AvaloniaProperty.Register<DesignerThumb, bool>("ThumbVisible", SharedInstances.BoxedTrue);

		/// <summary>
		/// Dependency property for <see cref="OperationMenu"/>.
		/// </summary>
		public static readonly StyledProperty<Control[]> OperationMenuProperty =
			AvaloniaProperty.Register<DesignerThumb, Control[]>("OperationMenu",null);

		public PlacementAlignment Alignment;
		
		protected override Type StyleKeyOverride => typeof(DesignerThumb);

		public void ReDraw()
		{
			var parent = this.TryFindParent<TemplatedControl>();
			if (parent != null)
				parent.InvalidateArrange();
		}

		/// <summary>
		/// Gets/Sets if the resize thumb is attached to the primary selection.
		/// </summary>
		public bool IsPrimarySelection {
			get { return (bool)GetValue(IsPrimarySelectionProperty); }
			set { SetValue(IsPrimarySelectionProperty, value); }
		}
		
		/// <summary>
		/// Gets/Sets if the resize thumb is visible.
		/// </summary>
		public bool ThumbVisible {
			get { return (bool)GetValue(ThumbVisibleProperty); }
			set { SetValue(ThumbVisibleProperty, value); }
		}

		/// <summary>
		/// Gets/Sets the OperationMenu.
		/// </summary>
		public Control[] OperationMenu
		{
			get { return (Control[])GetValue(OperationMenuProperty); }
			set { SetValue(OperationMenuProperty, value); }
		}
	}
}
