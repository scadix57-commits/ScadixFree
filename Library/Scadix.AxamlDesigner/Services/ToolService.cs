 

using Avalonia;
using Avalonia.Media;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.Services
{
	// See IToolService for description.
	sealed class DefaultToolService : IToolService, IDisposable
	{
		ITool _currentTool;
		IDesignPanel _designPanel;
		
		public DefaultToolService(DesignContext context)
		{
			_currentTool = this.PointerTool;
			context.Services.RunWhenAvailable(
				delegate(IDesignPanel designPanel) {
					_designPanel = designPanel;
					_currentTool.Activate(designPanel);
				});
		}
		
		public Func<Visual, bool> DesignPanelHitTestFilterCallback
		{
			set {
				if (_designPanel is DesignPanel dp)
					dp.CustomHitTestFilterBehavior = value;
			}
		}
		
		public void Dispose()
		{
			if (_designPanel != null) {
				_currentTool.Deactivate(_designPanel);
				_designPanel = null;
			}
		}
		
		public ITool PointerTool {
			get { return Services.PointerTool.Instance; }
		}
		
		public ITool CurrentTool {
			get { return _currentTool; }
			set {
				if (value == null)
					throw new ArgumentNullException("value");
				if (_currentTool == value) return;
				if (_designPanel != null) {
					_currentTool.Deactivate(_designPanel);
				}
				_currentTool = value;
				if (_designPanel != null) {
					_currentTool.Activate(_designPanel);
				}
				if (CurrentToolChanged != null) {
					CurrentToolChanged(this, EventArgs.Empty);
				}
			}
		}
		
		public event EventHandler CurrentToolChanged;

		public IDesignPanel DesignPanel {
			get { return _designPanel; }
		}
	}
}
