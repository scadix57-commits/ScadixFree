

using System.Diagnostics;
using Avalonia;
using Avalonia.Input;

namespace Scadix.AxamlDesigner.Services
{
	/// <summary>
	/// Base class for mouse gestures that should start dragging only after a minimum drag distance.
	/// </summary>
	public abstract class ClickOrDragMouseGesture : MouseGestureBase
	{
		protected Point startPoint;
		protected bool hasDragStarted;
		protected IInputElement positionRelativeTo;
		
		const double MinimumDragDistance = 3;
		
		protected sealed override void OnStarted(PointerPressedEventArgs e)
		{
			Debug.Assert(positionRelativeTo != null);
			hasDragStarted = false;
			startPoint = e.GetPosition(positionRelativeTo as Visual);
		}
		
		protected override void OnMouseMove(object sender, PointerEventArgs e)
		{
			if (!hasDragStarted) {
				Vector v = e.GetPosition(positionRelativeTo as Visual) - startPoint;
				// Use fixed minimum drag distance (4px) as Avalonia equivalent of SystemParameters
				const double MinHorizontalDrag = 4;
				const double MinVerticalDrag = 4;
				if (Math.Abs(v.X) >= MinHorizontalDrag
				    || Math.Abs(v.Y) >= MinVerticalDrag) {
					hasDragStarted = true;
					OnDragStarted(e);
				}
			}
		}
		
		protected override void OnStopped()
		{
			hasDragStarted = false;
		}
		
		protected virtual void OnDragStarted(PointerEventArgs e) {}
	}
}
