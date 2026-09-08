using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Scadix.AxamlDesign;

namespace Scadix.AxamlExpressionBlendInteractionAddon.TriggersEditor
{
	public class TriggersEditor : TemplatedControl
	{
		private ListBox? partListBox;

		static TriggersEditor()
		{
			// In Avalonia, we use StyleKeyProperty to override the default style key
		}

		protected override Type StyleKeyOverride => typeof(TriggersEditor);

		public static readonly StyledProperty<IEnumerable<DesignItem>> SelectedItemsProperty =
			AvaloniaProperty.Register<TriggersEditor, IEnumerable<DesignItem>>(nameof(SelectedItems));

		public IEnumerable<DesignItem> SelectedItems
		{
			get => GetValue(SelectedItemsProperty);
			set => SetValue(SelectedItemsProperty, value);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == SelectedItemsProperty) {
				UpdateTriggers();
			}
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			partListBox = e.NameScope.Find<ListBox>("PART_ListBox");
			if (partListBox != null)
			{
				partListBox.DoubleTapped += PartListBox_DoubleTapped;
				UpdateTriggers();
			}
		}

		private void PartListBox_DoubleTapped(object? sender, TappedEventArgs e)
		{
			var designerItem = partListBox?.SelectedItem as DesignItem;
			if (designerItem != null) {
				designerItem.Services.Selection.SetSelectedComponents(new[] { designerItem });
			}
		}

		private void UpdateTriggers()
		{
			if (partListBox != null) {
				partListBox.ItemsSource = null;
				if (SelectedItems != null && SelectedItems.Any()) {
					partListBox.ItemsSource = InteractionHelper.GetTriggers(SelectedItems);
				}
			}
		}
	}
}
