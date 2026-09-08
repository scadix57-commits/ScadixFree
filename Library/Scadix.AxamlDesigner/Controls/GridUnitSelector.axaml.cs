 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// Interaction logic for GridUnitSelector.xaml
	/// </summary>
	public partial class GridUnitSelector: UserControl
    {
		GridRailAdorner rail;

		public GridUnitSelector(GridRailAdorner rail)
		{
			InitializeComponent();

			this.rail = rail;
		}

		void FixedChecked(object sender, RoutedEventArgs e)
		{
			 
				this.rail.SetGridLengthUnit(Unit);
		}

		void StarChecked(object sender, RoutedEventArgs e)
		{
		 
				this.rail.SetGridLengthUnit(Unit);
		}

		void AutoChecked(object sender, RoutedEventArgs e)
		{
			 
				this.rail.SetGridLengthUnit(Unit);
		}

		public static readonly StyledProperty<Orientation> OrientationProperty =
			AvaloniaProperty.Register<GridUnitSelector, Orientation>("Orientation");

		public Orientation Orientation
		{
			get { return (Orientation)GetValue(OrientationProperty); }
			set { SetValue(OrientationProperty, value); }
		}

		public DesignItem SelectedItem { get; set; }

		public GridUnitType Unit
		{
			get
			{
				if (auto.IsChecked == true)
					return GridUnitType.Auto;
				if (star.IsChecked == true)
					return GridUnitType.Star;

				return GridUnitType.Pixel;
			}
			set
			{
				switch (value)
				{
					case GridUnitType.Auto:
						auto.IsChecked = true;
						break;
					case GridUnitType.Star:
						star.IsChecked = true;
						break;
					default:
                        fixed1.IsChecked = true;
						break;
				}
			}

		}
		protected override void OnPointerExited(PointerEventArgs e)
		{
			base.OnPointerExited(e);
			this.IsVisible = false;
		}
	}

}
