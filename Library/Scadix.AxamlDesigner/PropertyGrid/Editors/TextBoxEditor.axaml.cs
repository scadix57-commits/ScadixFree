 

using Avalonia.Data;
using Avalonia.Input;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
	public partial class TextBoxEditor: ClearableTextBox
    {
		/// <summary>
		/// Creates a new TextBoxEditor instance.
		/// </summary>
		public TextBoxEditor()
		{
			InitializeComponent();
		}
		
		/// <inheritdoc/>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Enter) {
				// In Avalonia, bindings update automatically on LostFocus for TwoWay
				// Force update by raising property changed
				SelectAll();
			} else if (e.Key == Key.Escape) {
				// Revert handled by base
			}
			base.OnKeyDown(e);
		}
	}
}
