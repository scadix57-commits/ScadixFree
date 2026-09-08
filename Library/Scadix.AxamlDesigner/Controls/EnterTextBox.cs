

using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;

namespace Scadix.AxamlDesigner.Controls
{
	public class EnterTextBox : TextBox
	{
        protected override Type StyleKeyOverride => typeof(TextBox);
        protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Enter) {
				var b = BindingOperations.GetBindingExpressionBase(this, TextProperty);
				if (b != null) {
					b.UpdateSource();
				}
				SelectAll();
			}
			else if (e.Key == Key.Escape) {
				var b = BindingOperations.GetBindingExpressionBase(this, TextProperty);
				if (b != null) {
					b.UpdateTarget();
				}
			}
			else {
				base.OnKeyDown(e);
			}
		}
	}
}
