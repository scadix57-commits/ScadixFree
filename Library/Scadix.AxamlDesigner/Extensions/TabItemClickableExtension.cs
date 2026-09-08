 
using Avalonia.Controls;
using Scadix.AxamlDesign.Extensions;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// Makes TabItems clickable.
	/// </summary>
	[ExtensionFor(typeof(Control))]
	[ExtensionServer(typeof(PrimarySelectionExtensionServer))]
	public sealed class TabItemClickableExtension : DefaultExtension
	{
		/// <summary/>
		protected override void OnInitialized()
		{
			// When tab item becomes primary selection, make it the active tab page in its parent tab control.
			var t = this.ExtendedItem;
			while (t != null) {
				if (t.Component is TabItem) {
					var tabItem = (TabItem) t.Component;
					var tabControl = tabItem.Parent as TabControl;
					if (tabControl != null) {
						tabControl.SelectedItem = tabItem;
					}
				}
				t = t.Parent;
			}
		}
	}
}
