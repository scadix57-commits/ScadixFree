
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// A ComboBox wich is Nullable
	/// </summary>
	public class NullableComboBox : ComboBox
	{
		protected override Type StyleKeyOverride => typeof(NullableComboBox);
		
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			var btn = e.NameScope.Find<Button>("PART_ClearButton");
			if (btn != null)
				btn.Click += btn_Click;
		}

		void btn_Click(object sender, RoutedEventArgs e)
		{
			var clearButton = (Button)sender;
			// Find parent ComboBox via visual tree
			var comboBox = clearButton.GetVisualAncestors().OfType<ComboBox>().FirstOrDefault();
			if (comboBox != null)
				comboBox.SelectedIndex = -1;
		}

		public bool IsNullable
		{
			get { return (bool)GetValue(IsNullableProperty); }
			set { SetValue(IsNullableProperty, value); }
		}

		public static readonly StyledProperty<bool> IsNullableProperty =
			AvaloniaProperty.Register<NullableComboBox, bool>("IsNullable", true);
	}
}
