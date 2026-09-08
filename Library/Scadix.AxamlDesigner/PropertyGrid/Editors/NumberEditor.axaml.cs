using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.PropertyGrid;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
	[TypeEditor(typeof(byte))]
	[TypeEditor(typeof(sbyte))]
	[TypeEditor(typeof(decimal))]
	[TypeEditor(typeof(double))]
	[TypeEditor(typeof(float))]
	[TypeEditor(typeof(int))]
	[TypeEditor(typeof(uint))]
	[TypeEditor(typeof(long))]
	[TypeEditor(typeof(ulong))]
	[TypeEditor(typeof(short))]
	[TypeEditor(typeof(ushort))]
	[TypeEditor(typeof(byte?))]
	[TypeEditor(typeof(sbyte?))]
	[TypeEditor(typeof(decimal?))]
	[TypeEditor(typeof(double?))]
	[TypeEditor(typeof(float?))]
	[TypeEditor(typeof(int?))]
	[TypeEditor(typeof(uint?))]
	[TypeEditor(typeof(long?))]
	[TypeEditor(typeof(ulong?))]
	[TypeEditor(typeof(short?))]
	[TypeEditor(typeof(ushort?))]
	public partial class NumberEditor: Scadix.AxamlDesigner.Controls.NumericUpDown
    {
		static NumberEditor()
		{
			minimums[typeof(byte)] = byte.MinValue;
			minimums[typeof(sbyte)] = sbyte.MinValue;
			minimums[typeof(decimal)] = (double)decimal.MinValue;
			minimums[typeof(double)] = double.MinValue;
			minimums[typeof(float)] = float.MinValue;
			minimums[typeof(int)] = int.MinValue;
			minimums[typeof(uint)] = uint.MinValue;
			minimums[typeof(long)] = long.MinValue;
			minimums[typeof(ulong)] = ulong.MinValue;
			minimums[typeof(short)] = short.MinValue;
			minimums[typeof(ushort)] = ushort.MinValue;

			maximums[typeof(byte)] = byte.MaxValue;
			maximums[typeof(sbyte)] = sbyte.MaxValue;
			maximums[typeof(decimal)] = (double)decimal.MaxValue;
			maximums[typeof(double)] = double.MaxValue;
			maximums[typeof(float)] = float.MaxValue;
			maximums[typeof(int)] = int.MaxValue;
			maximums[typeof(uint)] = uint.MaxValue;
			maximums[typeof(long)] = long.MaxValue;
			maximums[typeof(ulong)] = ulong.MaxValue;
			maximums[typeof(short)] = short.MaxValue;
			maximums[typeof(ushort)] = ushort.MaxValue;
		}

		public NumberEditor()
		{
			InitializeComponent();
			DataContextChanged +=  NumberEditor_DataContextChanged;
		}

        private void NumberEditor_DataContextChanged(object? sender, EventArgs e)
        {
            if (PropertyNode == null) return;
            var type = PropertyNode.FirstProperty.ReturnType;

            var range = Metadata.GetValueRange(PropertyNode.FirstProperty);
            if (range == null)
            {
                range = new NumberRange() { Min = double.MinValue, Max = double.MaxValue };
            }

            var nType = type;
            if (Nullable.GetUnderlyingType(type) != null)
            {
                nType = Nullable.GetUnderlyingType(type);
            }

            if (range.Min == double.MinValue)
            {
                Minimum = minimums[nType];
            }
            else
            {
                Minimum = range.Min;
            }

            if (range.Max == double.MaxValue)
            {
                Maximum = maximums[nType];
            }
            else
            {
                Maximum = range.Max;
            }

            if (type == typeof(double) || type == typeof(decimal))
            {
                DecimalPlaces = 2;
            }

            //			if (Minimum == 0 && Maximum == 1) {
            //				DecimalPlaces = 2;
            //				SmallChange = 0.01;
            //				LargeChange = 0.1;
            //			}
            //			else {
            //				ClearValue(DecimalPlacesProperty);
            //				ClearValue(SmallChangeProperty);
            //				ClearValue(LargeChangeProperty);
            //			}
        }

        static Dictionary<Type, double> minimums = new Dictionary<Type, double>();
		static Dictionary<Type, double> maximums = new Dictionary<Type, double>();

		public PropertyNode PropertyNode {
			get { return DataContext as PropertyNode; }
		}
 
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			TextBox textBox = e.NameScope.Find<TextBox>("PART_TextBox");
			if(textBox!=null)
				textBox.TextChanged += TextValueChanged;
		}
		
		private void TextValueChanged(object sender, TextChangedEventArgs e)
		{
			TextBox textBox = sender as TextBox;
			if(PropertyNode==null)
				return;
			if(textBox==null)
				return;
			double val;

			if(double.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
			{
				if (IsValidTypeConverter(PropertyNode.FirstProperty.TypeConverter, textBox.Text))
				{
					if (val >= Minimum && val <= Maximum || double.IsNaN(val))
					{
						textBox.Foreground = Brushes.Black;
						ToolTip.SetTip(textBox, textBox.Text);
					}
					else
					{
						textBox.Foreground = Brushes.DarkBlue;
						ToolTip.SetTip(textBox, "Value should be in between " + Minimum + " and " + Maximum);
					}
				}
				else
				{
					textBox.Foreground = Brushes.DarkRed;
					ToolTip.SetTip(textBox, "Cannot convert to Type : " + PropertyNode.FirstProperty.ReturnType.Name);
				}
			}
			else
			{
				textBox.Foreground = Brushes.DarkRed;
				ToolTip.SetTip(textBox, string.IsNullOrWhiteSpace(textBox.Text) ? null : "Value does not belong to any numeric type");
			}
		}

		// Method used instead of System.ComponentModel.TypeConverter.IsValid()
		// This ensures that TypeConverter is validated based on the current culture
		// See: https://stackoverflow.com/questions/16837774/typeconverter-isvalid-uses-current-thread-culture-but-typeconverter-convertfro
		private static bool IsValidTypeConverter(TypeConverter typeConverter, object value)
		{
			bool isValid = true;
			try
			{
				if (value == null || typeConverter.CanConvertFrom(value.GetType()))
				{
					typeConverter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
				}
				else
				{
					isValid = false;
				}
			}
			catch
			{
				isValid = false;
			}
			return isValid;
		}

		ChangeGroup group;

		protected override void OnDragStarted()
		{
			group = PropertyNode.Context.OpenGroup("drag number",
			                                       PropertyNode.Properties.Select(p => p.DesignItem).ToArray());
		}

		protected override void OnDragCompleted()
		{
			group.Commit();
		}
	}
}
