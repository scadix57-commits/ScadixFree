 

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Scadix.AxamlDesigner.Controls
{
	public class NumericUpDown : TemplatedControl
	{
		protected override Type StyleKeyOverride => typeof(NumericUpDown);
		
		TextBox textBox;
		DragRepeatButton upButton;
		DragRepeatButton downButton;

		public static readonly StyledProperty<int> DecimalPlacesProperty =
			AvaloniaProperty.Register<NumericUpDown, int>("DecimalPlaces");

		public int DecimalPlaces {
			get { return (int)GetValue(DecimalPlacesProperty); }
			set { SetValue(DecimalPlacesProperty, value); }
		}

		public static readonly StyledProperty<double> MinimumProperty =
			AvaloniaProperty.Register<NumericUpDown, double>("Minimum");

		public double Minimum {
			get { return (double)GetValue(MinimumProperty); }
			set { SetValue(MinimumProperty, value); }
		}

		public static readonly StyledProperty<double> MaximumProperty =
			AvaloniaProperty.Register<NumericUpDown, double>("Maximum", 100.0);

		public double Maximum {
			get { return (double)GetValue(MaximumProperty); }
			set { SetValue(MaximumProperty, value); }
		}

		public static readonly StyledProperty<double?> ValueProperty =
			AvaloniaProperty.Register<NumericUpDown, double?>("Value", SharedInstances.BoxedDouble0);

		public double? Value {
			get { return (double?)GetValue(ValueProperty); }
			set { SetValue(ValueProperty, value); }
		}

		public static readonly StyledProperty<double> SmallChangeProperty =
			AvaloniaProperty.Register<NumericUpDown, double>("SmallChange", SharedInstances.BoxedDouble1);

		public double SmallChange {
			get { return (double)GetValue(SmallChangeProperty); }
			set { SetValue(SmallChangeProperty, value); }
		}

		public static readonly StyledProperty<double> LargeChangeProperty =
			AvaloniaProperty.Register<NumericUpDown, double>("LargeChange", 10.0);

		public double LargeChange {
			get { return (double)GetValue(LargeChangeProperty); }
			set { SetValue(LargeChangeProperty, value); }
		}

		bool IsDragging {
			get {
				return upButton.IsDragging;
			}
			set {
				upButton.IsDragging = value; downButton.IsDragging = value;
			}
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			upButton = e.NameScope.Find<DragRepeatButton>("PART_UpButton");
			downButton = e.NameScope.Find<DragRepeatButton>("PART_DownButton");
			textBox = e.NameScope.Find<TextBox>("PART_TextBox");

			upButton.Click += upButton_Click;
			downButton.Click += downButton_Click;
			
			textBox.LostFocus += (sender, args) => OnLostFocus(e);

			var upDrag = new DragListener(upButton);
			var downDrag = new DragListener(downButton);

			upDrag.Started += drag_Started;
			upDrag.Changed += drag_Changed;
			upDrag.Completed += drag_Completed;

			downDrag.Started += drag_Started;
			downDrag.Changed += drag_Changed;
			downDrag.Completed += drag_Completed;

			Print();
		}

		void drag_Started(DragListener drag)
		{
			OnDragStarted();
		}

		void drag_Changed(DragListener drag)
		{
			IsDragging = true;
			MoveValue(-drag.DeltaDelta.Y * SmallChange);
		}

		void drag_Completed(DragListener drag)
		{
			IsDragging = false;
			OnDragCompleted();
		}

		void downButton_Click(object sender, RoutedEventArgs e)
		{
			if (!IsDragging) SmallDown();
		}

		void upButton_Click(object sender, RoutedEventArgs e)
		{
			if (!IsDragging) SmallUp();
		}

		protected virtual void OnDragStarted()
		{
		}

		protected virtual void OnDragCompleted()
		{
		}

		public void SmallUp()
		{
			MoveValue(SmallChange);
		}

		public void SmallDown()
		{
			MoveValue(-SmallChange);
		}

		public void LargeUp()
		{
			MoveValue(LargeChange);
		}

		public void LargeDown()
		{
			MoveValue(-LargeChange);
		}

		void MoveValue(double delta)
		{
			if (!Value.HasValue)
				return;

			double result;
			if (double.IsNaN((double)Value) || double.IsInfinity((double)Value)) {
				SetValue(delta);
			}
			else if (double.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
				SetValue(result + delta);
			}
			else {
				SetValue((double)Value + delta);
			}
		}

		void Print()
		{
			if (textBox != null)
			{
				textBox.Text = Value?.ToString("F" + DecimalPlaces, CultureInfo.InvariantCulture);
				textBox.CaretIndex = int.MaxValue;
			}
		}

		//wpf bug?: Value = -1 updates bindings without coercing, workaround
		//update: not derived from RangeBase - no problem
		void SetValue(double? newValue)
		{
			newValue = CoerceValue(newValue);
			if (Value != newValue && !(Value.HasValue && double.IsNaN(Value.Value) && newValue.HasValue && double.IsNaN(newValue.Value)))
				Value = newValue;
		}

		double? CoerceValue(double? newValue)
		{
			if (!newValue.HasValue)
				return null;

			return Math.Max(Minimum, Math.Min((double) newValue, Maximum));
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			switch (e.Key) {
				case Key.Enter:
					SetInputValue();
					textBox.SelectAll();
					e.Handled = true;
					break;
				case Key.Up:
					SmallUp();
					e.Handled = true;
					break;
				case Key.Down:
					SmallDown();
					e.Handled = true;
					break;
				case Key.PageUp:
					LargeUp();
					e.Handled = true;
					break;
				case Key.PageDown:
					LargeDown();
					e.Handled = true;
					break;
//				case Key.Home:
//					Maximize();
//					e.Handled = true;
//					break;
//				case Key.End:
//					Minimize();
//					e.Handled = true;
//					break;
			}
		}

		void SetInputValue()
		{
			double result;
			if (double.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
				SetValue(result);
			} else {
				Print();
			}
		}
		
		protected override void OnLostFocus(RoutedEventArgs e)
		{
			base.OnLostFocus(e);
			SetInputValue();
		}
		
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			if (e.Property == ValueProperty) {
				Value = CoerceValue((double?)e.NewValue);
				Print();
			}
			else if (e.Property == SmallChangeProperty &&
			         !this.IsSet(LargeChangeProperty)) {
				LargeChange = SmallChange * 10;
			}
		}
	}

	public class DragRepeatButton : RepeatButton
	{
		public static readonly StyledProperty<bool> IsDraggingProperty =
			AvaloniaProperty.Register<DragRepeatButton, bool>("IsDragging");

		public bool IsDragging {
			get { return (bool)GetValue(IsDraggingProperty); }
			set { SetValue(IsDraggingProperty, value); }
		}
	}
}
