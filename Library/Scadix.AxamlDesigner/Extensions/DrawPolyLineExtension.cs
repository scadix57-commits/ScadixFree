 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Services;

namespace Scadix.AxamlDesigner.Extensions
{
	[ExtensionFor(typeof(Canvas))]
	[ExtensionFor(typeof(Grid))]
	public class DrawPolyLineExtension : BehaviorExtension, IDrawItemExtension
	{
		DesignItem CreateItem(DesignContext context, Type componentType)
		{
			object newInstance = context.Services.ExtensionManager.CreateInstanceWithCustomInstanceFactory(componentType, null);
			DesignItem item = context.Services.Component.RegisterComponentForDesigner(newInstance);
			changeGroup = item.OpenGroup("Draw Line");
			context.Services.ExtensionManager.ApplyDefaultInitializers(item);
			return item;
		}

		private ChangeGroup changeGroup;

		#region IDrawItemBehavior implementation

		public bool CanItemBeDrawn(Type createItemType)
		{
			return createItemType == typeof(Polyline) || createItemType == typeof(Polygon);
		}

		public void StartDrawItem(DesignItem clickedOn, Type createItemType, IDesignPanel panel, PointerEventArgs e, Action<DesignItem> drawItemCallback)
		{
			var createdItem = CreateItem(panel.Context, createItemType);

			var startPoint = e.GetPosition(clickedOn.View as Avalonia.Visual);
			var operation = PlacementOperation.TryStartInsertNewComponents(clickedOn,
			                                                               new DesignItem[] { createdItem },
			                                                               new Rect[] { new Rect(startPoint.X, startPoint.Y, double.NaN, double.NaN) },
			                                                               PlacementType.AddItem);
			if (operation != null) {
				createdItem.Services.Selection.SetSelectedComponents(new DesignItem[] { createdItem });
				operation.Commit();
			}

			createdItem.Properties[Shape.StrokeProperty].SetValue(Brushes.Black);
			createdItem.Properties[Shape.StrokeThicknessProperty].SetValue(2d);
			createdItem.Properties[Shape.StretchProperty].SetValue(Stretch.None);
			if (drawItemCallback != null)
				drawItemCallback(createdItem);

			if (createItemType == typeof(Polyline))
				createdItem.Properties[Polyline.PointsProperty].CollectionElements.Add(createdItem.Services.Component.RegisterComponentForDesigner(new Point(0,0)));
			else
				createdItem.Properties[Polygon.PointsProperty].CollectionElements.Add(createdItem.Services.Component.RegisterComponentForDesigner(new Point(0,0)));
			
			new DrawPolylineMouseGesture(createdItem, clickedOn.View, changeGroup, this.ExtendedItem.GetCompleteAppliedTransformationToView()).Start(panel, (PointerPressedEventArgs)e);
		}

		#endregion
		
		sealed class DrawPolylineMouseGesture : ClickOrDragMouseGesture
		{
			private ChangeGroup changeGroup;
			private DesignItem newLine;
			private new Point startPoint;
			private Point? lastAdded;
			private Matrix matrix;

			public DrawPolylineMouseGesture(DesignItem newLine, IInputElement relativeTo, ChangeGroup changeGroup, Transform transform)
			{
				this.newLine = newLine;
				this.positionRelativeTo = relativeTo;
				this.changeGroup = changeGroup;
				this.matrix = transform.Value;
				matrix.Invert();

				startPoint = new Point();
			}
			
			protected override void OnPointerPressed(object sender, PointerPressedEventArgs e)
			{
				e.Handled = true;
				base.OnPointerPressed(sender, e);
			}
			
			protected override void OnMouseMove(object sender, PointerEventArgs e)
			{
				if (changeGroup == null)
					return;
				var rawDelta = matrix.Transform(e.GetPosition(null) - startPoint);
				double dx = rawDelta.X, dy = rawDelta.Y;
				if (lastAdded.HasValue) {
					dx = lastAdded.Value.X - rawDelta.X;
					dy = lastAdded.Value.Y - rawDelta.Y;
				}
				// Alt key check via stored modifiers - simplified
				var point = new Point(rawDelta.X, rawDelta.Y);

				if (newLine.View is Polyline) {
					if (((Polyline)newLine.View).Points.Count <= 1)
						((Polyline)newLine.View).Points.Add(point);
					if (false)
						((Polyline)newLine.View).Points.RemoveAt(((Polyline)newLine.View).Points.Count - 1);
					if (((Polyline)newLine.View).Points.Last() != point)
						((Polyline)newLine.View).Points.Add(point);
				} else {
					if (((Polygon)newLine.View).Points.Count <= 1)
						((Polygon)newLine.View).Points.Add(point);
					if (false)
						((Polygon)newLine.View).Points.RemoveAt(((Polygon)newLine.View).Points.Count - 1);
					if (((Polygon)newLine.View).Points.Last() != point)
						((Polygon)newLine.View).Points.Add(point);
				}
			}
			
			protected override void OnMouseUp(object sender, PointerReleasedEventArgs e)
			{
				if (changeGroup == null)
					return;

				var rawDelta = matrix.Transform(e.GetPosition(null) - startPoint);
				var point = new Point(rawDelta.X, rawDelta.Y);
				lastAdded = point;

				if (newLine.View is Polyline)
					((Polyline)newLine.View).Points.Add(point);
				else
					((Polygon)newLine.View).Points.Add(point);
			}
			
			protected override void OnMouseDoubleClick(object sender, PointerPressedEventArgs e)
			{
				base.OnMouseDoubleClick(sender, e);
				
				if (newLine.View is Polyline) {
					((Polyline)newLine.View).Points.RemoveAt(((Polyline)newLine.View).Points.Count - 1);
					// PointCollectionConverter not available - use string representation
					newLine.Properties[Polyline.PointsProperty].SetValue(string.Join(" ", ((Polyline)newLine.View).Points.Select(p => $"{p.X},{p.Y}")));
				} else {
					((Polygon)newLine.View).Points.RemoveAt(((Polygon)newLine.View).Points.Count - 1);
					newLine.Properties[Polygon.PointsProperty].SetValue(string.Join(" ", ((Polygon)newLine.View).Points.Select(p => $"{p.X},{p.Y}")));
				}
				
				if (changeGroup != null)
				{
					changeGroup.Commit();
					changeGroup = null;
				}
				
				Stop();
			}

			protected override void OnStopped()
			{
				if (changeGroup != null) {
					changeGroup.Abort();
					changeGroup = null;
				}
				if (services.Tool.CurrentTool is CreateComponentTool) {
					services.Tool.CurrentTool = services.Tool.PointerTool;
				}

				newLine.ReapplyAllExtensions();

				base.OnStopped();
			}
			
		}
	}
}
