 
using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.Services
{
	/// <summary>
	/// Mouse gesture for moving elements inside a container or between containers.
	/// Belongs to the PointerTool.
	/// </summary>
	public sealed class DragMoveMouseGesture : ClickOrDragMouseGesture
	{
		bool isDoubleClick;
		bool setSelectionIfNotMoving;
		MoveLogic moveLogic;

		public DragMoveMouseGesture(DesignItem clickedOn, bool isDoubleClick, bool setSelectionIfNotMoving = false)
		{
			Debug.Assert(clickedOn != null);
			
			this.isDoubleClick = isDoubleClick;
			this.setSelectionIfNotMoving = setSelectionIfNotMoving;
			this.positionRelativeTo = clickedOn.Services.DesignPanel;

			moveLogic = new MoveLogic(clickedOn);
		}
		
		protected override void OnDragStarted(PointerEventArgs e)
		{
			moveLogic.Start(startPoint);
		}
		
		protected override void OnMouseMove(object sender, PointerEventArgs e)
		{
			base.OnMouseMove(sender, e); // call OnDragStarted if min. drag distace is reached
			moveLogic.Move(e.GetPosition(positionRelativeTo as Visual));
		}
		
		protected override void OnMouseUp(object sender, PointerReleasedEventArgs e)
		{
			if (!hasDragStarted) {
				if (isDoubleClick) {
					// user made a double-click
					Debug.Assert(moveLogic.Operation == null);
					moveLogic.HandleDoubleClick();
				} else if (setSelectionIfNotMoving) {
					services.Selection.SetSelectedComponents(new DesignItem[] { moveLogic.ClickedOn }, SelectionTypes.Auto);
				}
			}
			moveLogic.Stop();
			Stop();
		}
		
		protected override void OnStopped()
		{
			moveLogic.Cancel();
		}
	}
}
