 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDesigner.Controls.Thumbs;
using Avalonia.VisualTree;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// The resize thumb around a component.
	/// </summary>
	[ExtensionServer(typeof(OnlyOneItemSelectedExtensionServer))]
	[ExtensionFor(typeof(Control))]
	public sealed class RotateThumbExtension : SelectionAdornerProvider
	{
		readonly AdornerPanel adornerPanel;
		readonly Thumb thumb;
		/// <summary>An array containing this.ExtendedItem as only element</summary>
		readonly DesignItem[] extendedItemArray = new DesignItem[1];
		IPlacementBehavior resizeBehavior;
		PlacementOperation operation;
		
		public RotateThumbExtension()
		{
			adornerPanel = new AdornerPanel();
			adornerPanel.Order = AdornerOrder.Foreground;
			this.Adorners.Add(adornerPanel);
			
			thumb = CreateRotateThumb();
		}
		
		DesignerThumb CreateRotateThumb()
		{
			DesignerThumb rotateThumb = new RotateThumb();
			rotateThumb.Cursor = new Cursor(StandardCursorType.Hand);
            rotateThumb.Cursor = ZoomControl.GetCursor("avares://Scadix.AxamlDesigner/Images/rotate.cur");
            rotateThumb.Alignment = PlacementAlignment.Top;
			AdornerPanel.SetPlacement(rotateThumb,
			                          new RelativePlacement(HorizontalAlignment.Center, VerticalAlignment.Top) { WidthRelativeToContentWidth = 1, HeightOffset = 0 });
			adornerPanel.Children.Add(rotateThumb);

			DragListener drag = new DragListener(rotateThumb);
			drag.Started += drag_Rotate_Started;
			drag.Changed += drag_Rotate_Changed;
			drag.Completed += drag_Rotate_Completed;
			return rotateThumb;
		}
		
		#region Rotate
		
		private Point centerPoint;
		private Control parent;
		private Vector startVector;
		private RotateTransform rotateTransform;
		private double initialAngle;
		private DesignItem rtTransform;
		
		private void drag_Rotate_Started(DragListener drag)
		{
			var designerItem = this.ExtendedItem.Component as Control;
			this.parent = (designerItem as Visual)?.GetVisualParent() as Control;

			// centerPoint in parent coordinates
			var originInItem = new Point(
				designerItem.Bounds.Width  * designerItem.RenderTransformOrigin.Point.X,
				designerItem.Bounds.Height * designerItem.RenderTransformOrigin.Point.Y);
			this.centerPoint = designerItem.TranslatePoint(originInItem, this.parent) ?? new Point();

			// Use e.GetPosition(parent) — exact equivalent of WPF Mouse.GetPosition(parent)
			var startPoint = drag.LastPointerEventArgs?.GetPosition(this.parent) ?? new Point();
			this.startVector = startPoint - this.centerPoint;

			if (this.rotateTransform == null)
				this.initialAngle = 0;
			else
				this.initialAngle = this.rotateTransform.Angle;

			rtTransform = this.ExtendedItem.Properties[Control.RenderTransformProperty].Value;
			operation = PlacementOperation.Start(extendedItemArray, PlacementType.Resize);
		}

		private void drag_Rotate_Changed(DragListener drag)
		{
			// Use e.GetPosition(parent) — exact equivalent of WPF Mouse.GetPosition(parent)
			var currentPoint = drag.LastPointerEventArgs?.GetPosition(this.parent) ?? new Point();
			Vector deltaVector = currentPoint - this.centerPoint;

			double angle = Math.Atan2(
				this.startVector.X * deltaVector.Y - this.startVector.Y * deltaVector.X,
				this.startVector.X * deltaVector.X + this.startVector.Y * deltaVector.Y
			) * 180.0 / Math.PI;

			var destAngle = this.initialAngle + Math.Round(angle, 0);

			// Snap to 15° unless Ctrl is held
			bool ctrlHeld = (drag.LastKeyModifiers & KeyModifiers.Control) != 0;
			if (!ctrlHeld)
				destAngle = ((int)destAngle / 15) * 15;

			ModelTools.ApplyTransform(this.ExtendedItem, new RotateTransform() { Angle = destAngle }, false);
		}

		void drag_Rotate_Completed(DragListener drag)
		{
			operation.Commit();
		}
		
		#endregion
		
		protected override void OnInitialized()
		{
			if (this.ExtendedItem.Component is WindowClone)
				return;
			base.OnInitialized();
			extendedItemArray[0] = this.ExtendedItem;
			this.ExtendedItem.PropertyChanged += OnPropertyChanged;
			this.Services.Selection.PrimarySelectionChanged += OnPrimarySelectionChanged;
			resizeBehavior = PlacementOperation.GetPlacementBehavior(extendedItemArray);
			OnPrimarySelectionChanged(null, null);
			
			var designerItem = this.ExtendedItem.Component as Control;
			this.rotateTransform = designerItem.RenderTransform as RotateTransform;
			if (this.rotateTransform == null) {
				var tg = designerItem.RenderTransform as TransformGroup;
				if (tg != null) {
					this.rotateTransform = tg.Children.FirstOrDefault(x => x is RotateTransform) as RotateTransform;
				}
			}
				
		}
		
		void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{ }
		
		protected override void OnRemove()
		{
			this.ExtendedItem.PropertyChanged -= OnPropertyChanged;
			this.Services.Selection.PrimarySelectionChanged -= OnPrimarySelectionChanged;
			base.OnRemove();
		}
		
		void OnPrimarySelectionChanged(object sender, EventArgs e)
		{
			bool isPrimarySelection = this.Services.Selection.PrimarySelection == this.ExtendedItem;
			foreach (RotateThumb g in adornerPanel.Children) {
				g.IsPrimarySelection = isPrimarySelection;
			}
		}
	}
}

