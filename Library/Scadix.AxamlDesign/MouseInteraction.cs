

using Avalonia.Input;

namespace Scadix.AxamlDesign
{
	// Interfaces for mouse interaction on the design surface.
	
	/// <summary>
	/// Behavior interface implemented by elements to handle the mouse down event
	/// on them.
	/// </summary>
	public interface IHandlePointerToolMouseDown
	{
		/// <summary>
		/// Called to handle the mouse down event.
		/// </summary>
		void HandleSelectionMouseDown(IDesignPanel designPanel, PointerPressedEventArgs e, DesignPanelHitTestResult result);
	}
}
