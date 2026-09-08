 
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
	public class DrawLineExtension : BehaviorExtension, IDrawItemExtension
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
			return createItemType == typeof(Line);
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

			var lineHandler = createdItem.Extensions.OfType<LineHandlerExtension>().First();
			lineHandler.DragListener.ExternalStart();
			
			new DrawLineMouseGesture(lineHandler, clickedOn.View, changeGroup).Start(panel, (PointerPressedEventArgs)e);
		}

		#endregion
		
		sealed class DrawLineMouseGesture : ClickOrDragMouseGesture
		{
			private LineHandlerExtension l;
			private ChangeGroup changeGroup;

			public DrawLineMouseGesture(LineHandlerExtension l, IInputElement relativeTo, ChangeGroup changeGroup)
			{
				this.l = l;
				this.positionRelativeTo = relativeTo;
				this.changeGroup = changeGroup;
			}
			
			protected override void OnMouseMove(object sender, PointerEventArgs e)
			{
				base.OnMouseMove(sender, e);
				l.DragListener.ExternalPointerMove(e);
			}
			
			protected override void OnMouseUp(object sender, PointerReleasedEventArgs e)
			{
				l.DragListener.ExternalStop();
				if (changeGroup != null)
				{
					changeGroup.Commit();
					changeGroup = null;
				}
				base.OnMouseUp(sender, e);
			}

			protected override void OnStopped()
			{
				if (changeGroup != null)
				{
					changeGroup.Abort();
					changeGroup = null;
				}
				if (services.Tool.CurrentTool is CreateComponentTool)
				{
					services.Tool.CurrentTool = services.Tool.PointerTool;
				}
				base.OnStopped();
			}
			
		}
	}
}
