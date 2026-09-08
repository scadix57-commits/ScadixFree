

using Avalonia;
using Avalonia.Controls.Primitives;

namespace Scadix.AxamlDesigner.Controls
{
	public class EnumButton : ToggleButton
	{
		protected override Type StyleKeyOverride => typeof(EnumButton);

		public static readonly StyledProperty<object> ValueProperty =
			AvaloniaProperty.Register<EnumButton, object>("Value");

		public object Value {
			get { return (object)GetValue(ValueProperty); }
			set { SetValue(ValueProperty, value); }
		}
	}
}
