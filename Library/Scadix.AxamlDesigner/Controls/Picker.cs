 
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Scadix.AxamlDesigner.Controls
{
	public class Picker : Grid
	{
		public Picker()
		{
			SizeChanged += delegate { UpdateValueOffset(); };
		}

		public static readonly StyledProperty<Control> MarkerProperty =
			AvaloniaProperty.Register<Picker, Control>("Marker");

		public Control Marker {
			get { return (Control)GetValue(MarkerProperty); }
			set { SetValue(MarkerProperty, value); }
		}

		public static readonly StyledProperty<double> ValueProperty =
			AvaloniaProperty.Register<Picker, double>("Value", SharedInstances.BoxedDouble0);

		public double Value {
			get { return (double)GetValue(ValueProperty); }
			set { SetValue(ValueProperty, value); }
		}

		public static readonly StyledProperty<double> ValueOffsetProperty =
			AvaloniaProperty.Register<Picker, double>("ValueOffset");

		public double ValueOffset {
			get { return (double)GetValue(ValueOffsetProperty); }
			set { SetValue(ValueOffsetProperty, value); }
		}

		public static readonly StyledProperty<Orientation> OrientationProperty =
			AvaloniaProperty.Register<Picker, Orientation>("Orientation");

		public Orientation Orientation {
			get { return (Orientation)GetValue(OrientationProperty); }
			set { SetValue(OrientationProperty, value); }
		}

		public static readonly StyledProperty<double> MinimumProperty =
			AvaloniaProperty.Register<Picker, double>("Minimum");

		public double Minimum {
			get { return (double)GetValue(MinimumProperty); }
			set { SetValue(MinimumProperty, value); }
		}

		public static readonly StyledProperty<double> MaximumProperty =
			AvaloniaProperty.Register<Picker, double>("Maximum", 100.0);

		public double Maximum {
			get { return (double)GetValue(MaximumProperty); }
			set { SetValue(MaximumProperty, value); }
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			if (e.Property == MarkerProperty) {
				TranslateTransform t = Marker.RenderTransform as TranslateTransform;
				if (t == null) {
					t = new TranslateTransform();
					Marker.RenderTransform = t;
				}
				var property = Orientation == Orientation.Horizontal ? TranslateTransform.XProperty : TranslateTransform.YProperty;
				t.Bind(property, new Binding("ValueOffset") { Source = this });
			}
			else if (e.Property == ValueProperty) {
				UpdateValueOffset();
			}
		}

		bool isMouseDown;
		private Point _lastPointerPosition;

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			isMouseDown = true;
			e.Pointer.Capture(this);
			_lastPointerPosition = e.GetPosition(this);
			UpdateValue();
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			if (isMouseDown) {
				_lastPointerPosition = e.GetPosition(this);
				UpdateValue();
			}
		}

		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			isMouseDown = false;
			e.Pointer.Capture(null);
		}

		void UpdateValue()
		{
			// Use last known pointer position via Bounds
			double length = 0, pos = 0;
			// Position is tracked via OnPointerMoved - use stored point
			Point p = _lastPointerPosition;
			
			if (Orientation == Orientation.Horizontal) {
				length = Bounds.Width;
				pos = p.X;
			}
			else {
				length = Bounds.Height;
				pos = p.Y;
			}

			pos = Math.Max(0, Math.Min(length, pos));
			Value = Minimum + (Maximum - Minimum) * pos / length;
		}

		void UpdateValueOffset()
		{
			var length = Orientation == Orientation.Horizontal ? Bounds.Width : Bounds.Height;
			ValueOffset = length * (Value - Minimum) / (Maximum - Minimum);
		}
	}
}
