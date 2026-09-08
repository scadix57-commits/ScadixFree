
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.Controls
{
	public partial class ColorPicker : UserControl
	{
		public ColorPicker()
		{
			InitializeComponent();
		}
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == ColorProperty)
				OnColorPropertyChanged();
			else if (change.Property == HProperty || change.Property == SProperty || change.Property == VProperty)
				OnHsvPropertyChanged();
			else if (change.Property == RProperty || change.Property == GProperty || change.Property == BProperty || change.Property == AProperty)
				OnRgbaPropertyChanged();
			else if (change.Property == HexProperty)
				OnHexPropertyChanged();
		}

		public static readonly StyledProperty<Color> ColorProperty =
			AvaloniaProperty.Register<ColorPicker, Color>("Color", new Color());

		public Color Color {
			get { return GetValue(ColorProperty); }
			set { SetValue(ColorProperty, value); }
		}

		public static readonly StyledProperty<int> HProperty =
			AvaloniaProperty.Register<ColorPicker, int>("H");

		public int H {
			get { return GetValue(HProperty); }
			set { SetValue(HProperty, value); }
		}

		public static readonly StyledProperty<int> SProperty =
			AvaloniaProperty.Register<ColorPicker, int>("S");

		public int S {
			get { return GetValue(SProperty); }
			set { SetValue(SProperty, value); }
		}

		public static readonly StyledProperty<int> VProperty =
			AvaloniaProperty.Register<ColorPicker, int>("V");

		public int V {
			get { return GetValue(VProperty); }
			set { SetValue(VProperty, value); }
		}

		public static readonly StyledProperty<byte> RProperty =
			AvaloniaProperty.Register<ColorPicker, byte>("R");

		public byte R {
			get { return GetValue(RProperty); }
			set { SetValue(RProperty, value); }
		}

		public static readonly StyledProperty<byte> GProperty =
			AvaloniaProperty.Register<ColorPicker, byte>("G");

		public byte G {
			get { return GetValue(GProperty); }
			set { SetValue(GProperty, value); }
		}

		public static readonly StyledProperty<byte> BProperty =
			AvaloniaProperty.Register<ColorPicker, byte>("B");

		public byte B {
			get { return GetValue(BProperty); }
			set { SetValue(BProperty, value); }
		}

		public static readonly StyledProperty<byte> AProperty =
			AvaloniaProperty.Register<ColorPicker, byte>("A");

		public byte A {
			get { return GetValue(AProperty); }
			set { SetValue(AProperty, value); }
		}

		public static readonly StyledProperty<string> HexProperty =
			AvaloniaProperty.Register<ColorPicker, string>("Hex");

		public string Hex {
			get { return GetValue(HexProperty); }
			set { SetValue(HexProperty, value); }
		}

		public static readonly StyledProperty<Color> HueColorProperty =
			AvaloniaProperty.Register<ColorPicker, Color>("HueColor");

		public Color HueColor {
			get { return GetValue(HueColorProperty); }
			set { SetValue(HueColorProperty, value); }
		}

		bool updating;

		void OnColorPropertyChanged()
		{
			if (updating) return;
			updating = true;
			try {
				UpdateSource(ColorSource.Hsv);
				UpdateRest(ColorSource.Hsv);
			} finally { updating = false; }
		}

		void OnHsvPropertyChanged()
		{
			if (updating) return;
			updating = true;
			try {
				var c = ColorHelper.ColorFromHsv(H, S / 100.0, V / 100.0);
				c = new Color(A, c.R, c.G, c.B);
				Color = c;
				UpdateRest(ColorSource.Hsv);
			} finally { updating = false; }
		}

		void OnRgbaPropertyChanged()
		{
			if (updating) return;
			updating = true;
			try {
				Color = Color.FromArgb(A, R, G, B);
				UpdateRest(ColorSource.Rgba);
			} finally { updating = false; }
		}

		void OnHexPropertyChanged()
		{
			if (updating) return;
			updating = true;
			try {
				Color = ColorHelper.ColorFromString(Hex);
				UpdateRest(ColorSource.Hex);
			} finally { updating = false; }
		}

		void UpdateRest(ColorSource source)
		{
			HueColor = ColorHelper.ColorFromHsv(H, 1, 1);
			UpdateSource((ColorSource)(((int)source + 1) % 3));
			UpdateSource((ColorSource)(((int)source + 2) % 3));
		}

		void UpdateSource(ColorSource source)
		{
			if (source == ColorSource.Hsv) {
				double h, s, v;
				ColorHelper.HsvFromColor(Color, out h, out s, out v);

				H = (int)h;
				S = (int)(s * 100);
				V = (int)(v * 100);
			}
			else if (source == ColorSource.Rgba) {
				R = Color.R;
				G = Color.G;
				B = Color.B;
				A = Color.A;
			}
			else {
				Hex = ColorHelper.StringFromColor(Color);
			}
		}

		enum ColorSource
		{
			Hsv, Rgba, Hex
		}
	}

	class HexTextBox : TextBox
	{
		protected override void OnKeyDown(KeyEventArgs e)
		{
            if (e.Key == Key.Enter)
            {
                var b = BindingOperations.GetBindingExpressionBase(this, TextProperty);
                if (b != null)
                {
                    b.UpdateTarget();
                }
                SelectAll();
            }
        }
	}
}
