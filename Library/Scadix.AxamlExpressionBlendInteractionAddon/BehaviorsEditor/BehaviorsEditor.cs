using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Scadix.AxamlDesign;

namespace Scadix.AxamlExpressionBlendInteractionAddon.BehaviorsEditor
{
	public class BehaviorsEditor : TemplatedControl
	{
		private ListBox? partListBox;

		static BehaviorsEditor()
		{
			// In Avalonia, we use StyleKeyProperty to override the default style key
		}

		protected override Type StyleKeyOverride => typeof(BehaviorsEditor);

		public static readonly StyledProperty<IEnumerable<DesignItem>> SelectedItemsProperty =
			AvaloniaProperty.Register<BehaviorsEditor, IEnumerable<DesignItem>>(nameof(SelectedItems));

		public IEnumerable<DesignItem> SelectedItems
		{
			get => GetValue(SelectedItemsProperty);
			set => SetValue(SelectedItemsProperty, value);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == SelectedItemsProperty) {
				UpdateBehaviors();
			}
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			partListBox = e.NameScope.Find<ListBox>("PART_ListBox");
			if (partListBox != null)
			{
				partListBox.DoubleTapped += PartListBox_DoubleTapped;
				UpdateBehaviors();
			}
		}

		private void PartListBox_DoubleTapped(object? sender, TappedEventArgs e)
		{
			var designerItem = partListBox?.SelectedItem as DesignItem;
			if (designerItem != null)
			{
				designerItem.Services.Selection.SetSelectedComponents(new[] { designerItem });
			}
		}

		private void UpdateBehaviors()
		{
			if (partListBox != null) {
				partListBox.ItemsSource = null;
				if (SelectedItems != null && SelectedItems.Any()) {
					partListBox.ItemsSource = InteractionHelper.GetBehaviors(SelectedItems);
				}
			}
		}
	}
}
