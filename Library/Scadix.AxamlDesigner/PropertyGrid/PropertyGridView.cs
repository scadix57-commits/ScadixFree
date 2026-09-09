

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.PropertyGrid;

namespace Scadix.AxamlDesigner.PropertyGrid
{
	[TemplatePart(Name = "PART_Thumb", Type = typeof(Thumb))]
	public class PropertyGridView : TemplatedControl
	{
		protected override Type StyleKeyOverride => typeof(PropertyGridView);
		
		public PropertyGridView() : this(null)
		{
		}
		
		public PropertyGridView(IPropertyGrid pg)
		{
			PropertyGrid = pg??new PropertyGrid();
			DataContext = PropertyGrid;
		}
		
		private Thumb thumb;
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			thumb = e.NameScope.Find<Thumb>("PART_Thumb");
			if (thumb != null)
				thumb.DragDelta += new EventHandler<VectorEventArgs>(thumb_DragDelta);
		}

		static PropertyContextMenu propertyContextMenu = new PropertyContextMenu();

		public IPropertyGrid PropertyGrid { get; private set; }

        public static readonly StyledProperty<bool> IsReadOnlyProperty =
            AvaloniaProperty.Register<PropertyGridView, bool>(nameof(IsReadOnly));

        public bool IsReadOnly
        {
            get => GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly StyledProperty<bool> AllowAdvancedEditingProperty =
            AvaloniaProperty.Register<PropertyGridView, bool>(nameof(AllowAdvancedEditing), true);

        public bool AllowAdvancedEditing
        {
            get => GetValue(AllowAdvancedEditingProperty);
            set => SetValue(AllowAdvancedEditingProperty, value);
        }

		public static readonly StyledProperty<double> FirstColumnWidthProperty =
			AvaloniaProperty.Register<PropertyGridView, double>("FirstColumnWidth", 120.0);

		public double FirstColumnWidth {
			get { return GetValue(FirstColumnWidthProperty); }
			set { SetValue(FirstColumnWidthProperty, value); }
		}

		public static readonly StyledProperty<IEnumerable<DesignItem>> SelectedItemsProperty =
			AvaloniaProperty.Register<PropertyGridView, IEnumerable<DesignItem>>("SelectedItems");

		public IEnumerable<DesignItem> SelectedItems {
			get { return GetValue(SelectedItemsProperty); }
			set { SetValue(SelectedItemsProperty, value); }
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == SelectedItemsProperty) {
				PropertyGrid.SelectedItems = SelectedItems;
			}
		}

		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			base.OnPointerReleased(e);
			if (!IsReadOnly && AllowAdvancedEditing && e.InitialPressMouseButton == MouseButton.Right) {
				var ancestors = (e.Source as AvaloniaObject).GetVisualAncestors();
				Border row = ancestors.OfType<Border>().FirstOrDefault(b => b.Name == "uxPropertyNodeRow");
				if (row == null) return;

				PropertyNode node = row.DataContext as PropertyNode;
				if (node.IsEvent) return;

				PropertyContextMenu contextMenu = new PropertyContextMenu();
				contextMenu.DataContext = node;
                contextMenu.OpenAt(row, node);
            }
		}

		void thumb_DragDelta(object sender, VectorEventArgs e)
		{
			FirstColumnWidth = Math.Max(0, FirstColumnWidth + e.Vector.X);
		}
	}
}
