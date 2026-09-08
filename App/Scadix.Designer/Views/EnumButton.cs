using Avalonia;
using Avalonia.Controls.Primitives;
using Scadix.AxamlDesigner;
using System;

namespace Scadix.Designer
{
	public class EnumButton : ToggleButton
	{
        protected override Type StyleKeyOverride => typeof(EnumButton);
       
		public static readonly StyledProperty<object?> ValueProperty =
			AvaloniaProperty.Register<EnumButton, object?>(nameof(Value));

		public object? Value {
			get { return GetValue(ValueProperty); }
			set { SetValue(ValueProperty, value); }
		}
	}
}
