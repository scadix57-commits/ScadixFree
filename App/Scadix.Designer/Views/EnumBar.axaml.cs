using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System;
using System.Linq;

namespace Scadix.Designer
{
	public partial class EnumBar : UserControl
	{
		public EnumBar()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private Type? currentEnumType;

		public static readonly StyledProperty<object?> ValueProperty =
			AvaloniaProperty.Register<EnumBar, object?>(nameof(Value));

		public object? Value {
			get { return GetValue(ValueProperty); }
			set { SetValue(ValueProperty, value); }
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			if (e.Property == ValueProperty) {
                var newValue = e.GetNewValue<object?>();
                if (newValue == null) return;
                
				var type = newValue.GetType();

				if (currentEnumType != type) {
					currentEnumType = type;
					uxPanel.Children.Clear();
					foreach (var v in Enum.GetValues(type)) {
						var b = new EnumButton();
						b.Value = v;
						b.Content = Enum.GetName(type, v);
						b.PointerPressed += button_PointerPressed;
						uxPanel.Children.Add(b);
					}
				}

				foreach (var child in uxPanel.Children) {
                    if (child is EnumButton c)
                    {
					    c.IsChecked = c.Value?.Equals(Value) ?? false;
                    }
				}
			}
		}

		private void button_PointerPressed(object? sender, PointerPressedEventArgs e)
		{
            if (sender is EnumButton b)
            {
			    Value = b.Value;
			    e.Handled = true;
            }
		}
	}
}
