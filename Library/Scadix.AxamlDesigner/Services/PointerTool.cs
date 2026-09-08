 

using Avalonia.Input;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.Services
{
	sealed class PointerTool : ITool
	{
		internal static readonly PointerTool Instance = new PointerTool();
		
		public Cursor Cursor {
			get { return null; }
		}
		
		public void Activate(IDesignPanel designPanel)
		{
			designPanel.PointerPressed += OnMouseDown;
		}
		
		public void Deactivate(IDesignPanel designPanel)
		{
			designPanel.PointerPressed -= OnMouseDown;
		}
		
		void OnMouseDown(object sender, PointerPressedEventArgs e)
		{
			IDesignPanel designPanel = (IDesignPanel)sender;
			DesignPanelHitTestResult result = designPanel.HitTest(e.GetPosition(designPanel as Avalonia.Visual), false, true, HitTestType.ElementSelection);
			if (result.ModelHit != null) {
				IHandlePointerToolMouseDown b = result.ModelHit.GetBehavior<IHandlePointerToolMouseDown>();
				if (b != null) {
					b.HandleSelectionMouseDown(designPanel, e, result);
				}
				if (!e.Handled) {
					var props = e.GetCurrentPoint(null).Properties;
					if (props.IsLeftButtonPressed && MouseGestureBase.IsOnlyButtonPressed(e, MouseButton.Left)) {
						e.Handled = true;
						ISelectionService selectionService = designPanel.Context.Services.Selection;
						bool setSelectionIfNotMoving = false;
						if (selectionService.IsComponentSelected(result.ModelHit)) {
							setSelectionIfNotMoving = true;
						} else {
							selectionService.SetSelectedComponents(new DesignItem[] { result.ModelHit }, SelectionTypes.Auto);
						}
						if (selectionService.IsComponentSelected(result.ModelHit)) {
							new DragMoveMouseGesture(result.ModelHit, e.ClickCount == 2, setSelectionIfNotMoving).Start(designPanel, e);
						}
					}
				}
			}
		}
	}
}
