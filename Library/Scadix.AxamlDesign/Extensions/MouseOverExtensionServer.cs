
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scadix.AxamlDesign.Adorners;

namespace Scadix.AxamlDesign.Extensions
{
	/// <summary>
	/// Applies an extension to the hovered components.
	/// </summary>
	public class MouseOverExtensionServer : DefaultExtensionServer
	{
		private DesignItem _lastItem = null;

		/// <summary>
		/// Is called after the extension server is initialized and the Context property has been set.
		/// </summary>
		protected override void OnInitialized()
		{
			base.OnInitialized();
			var panel = this.Services.GetService<IDesignPanel>() as Control;
			if (panel != null)
			{
				((Control)this.Services.DesignPanel).PointerMoved += MouseOverExtensionServer_PreviewMouseMove;
				((Control)this.Services.DesignPanel).PointerExited += MouseOverExtensionServer_MouseLeave;
				Services.Selection.SelectionChanged += OnSelectionChanged;
			}
		}

		void OnSelectionChanged(object sender, DesignItemCollectionEventArgs e)
		{
			ReapplyExtensions(e.Items);
		}

		private void MouseOverExtensionServer_MouseLeave(object sender, PointerEventArgs e)
		{
			if (_lastItem != null)
			{
				var oldLastItem = _lastItem;
				_lastItem = null;
				ReapplyExtensions(new[] { oldLastItem });
			}
		}

		private void MouseOverExtensionServer_PreviewMouseMove(object sender, PointerEventArgs e)
		{
            DesignItem element = null;
            var panel = (Control)Services.DesignPanel;
            var position = e.GetPosition(panel);

            // Simple hit testing in Avalonia
            var hitTest = panel.InputHitTest(position);
            if (hitTest is Visual visual)
            {
                // Walk up the visual tree to find design items
                var current = visual;
                while (current != null)
                {
                    if (current is IAdornerLayer)
                    {
                        current = current.GetVisualParent();
                        continue;
                    }

                    if (Extension.GetDisableMouseOverExtensions(current))
                    {
                        current = current.GetVisualParent();
                        continue;
                    }

                    var item = Services.Component.GetDesignItem(current);
                    if (item != null)
                    {
                        element = item;
                        break;
                    }

                    current = current.GetVisualParent();
                }
            }

            var oldLastItem = _lastItem;
            _lastItem = element;
            if (oldLastItem != null && oldLastItem != element)
                ReapplyExtensions(new[] { oldLastItem, element });
            else
                ReapplyExtensions(new[] { element });
        }

		/// <summary>
		/// Gets if the item is selected.
		/// </summary>
		public override bool ShouldApplyExtensions(DesignItem extendedItem)
		{
			return extendedItem == _lastItem && !Services.Selection.IsComponentSelected(extendedItem);
		}
	}
}
