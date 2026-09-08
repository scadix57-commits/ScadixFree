

using Avalonia;
using Avalonia.Controls;
using Scadix.AxamlDesign.PropertyGrid;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
	[TypeEditor(typeof(TimeSpan))]
	public partial class TimeSpanEditor :UserControl
	{
		public TimeSpanEditor()
		{
			InitializeComponent();
			DataContextChanged += NumberEditor_DataContextChanged;

            NeagtiveProperty.Changed.AddClassHandler<TimeSpanEditor>((x, e) => OnNeagtivePropertyChanged(x, e));
            DaysProperty.Changed.AddClassHandler<TimeSpanEditor>((x, e) => OnDaysPropertyChanged(x, e));
            HoursProperty.Changed.AddClassHandler<TimeSpanEditor>((x, e) => OnHoursPropertyChanged(x, e));
			MinutesProperty.Changed.AddClassHandler<TimeSpanEditor>((x, e) => OnMinutesPropertyChanged(x, e));
			SecondsProperty.Changed.AddClassHandler<TimeSpanEditor>((x, e) => OnSecondsPropertyChanged(x, e));
			MiliSecondsProperty.Changed.AddClassHandler<TimeSpanEditor>((x, e) => OnMiliSecondsPropertyChanged(x, e));
        }

        private void NumberEditor_DataContextChanged(object? sender, EventArgs e)
        {
            if (PropertyNode == null)
                return;

            var designerValue = PropertyNode.DesignerValue;
            if (designerValue == null)
                return;

            var value = (TimeSpan)designerValue;


            if (value < TimeSpan.Zero)
            {
                this.Neagtive = true;
                value = value.Negate();
            }
            this.Days = value.Days;
            this.Hours = value.Hours;
            this.Minutes = value.Minutes;
            this.Seconds = value.Seconds;
            this.MiliSeconds = value.Milliseconds;
        }

        public PropertyNode PropertyNode
		{
			get { return DataContext as PropertyNode; }
		}

	 

		private void UpdateValue()
		{
			var ts = new TimeSpan(this.Days, this.Hours, this.Minutes, this.Seconds, this.MiliSeconds);
			if (this.Neagtive)
				ts = ts.Negate();
			PropertyNode.DesignerValue = ts;
		}

		public bool Neagtive
		{
			get { return (bool)GetValue(NeagtiveProperty); }
			set { SetValue(NeagtiveProperty, value); }
		}
		 
		// Using a AvaloniaProperty as the backing store for Neagtive.  This enables animation, styling, binding, etc...
		public static readonly StyledProperty<bool> NeagtiveProperty =
			AvaloniaProperty.Register<TimeSpanEditor, bool>("Neagtive", false);

		private static void OnNeagtivePropertyChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
		{
			var ctl = (TimeSpanEditor)d;
			ctl.UpdateValue();
		}


		public int Days
		{
			get { return (int)GetValue(DaysProperty); }
			set { SetValue(DaysProperty, value); }
		}

		// Using a AvaloniaProperty as the backing store for Days.  This enables animation, styling, binding, etc...
		public static readonly StyledProperty<int> DaysProperty =
			AvaloniaProperty.Register<TimeSpanEditor, int>("Days", 0);

		private static void OnDaysPropertyChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
		{
			var ctl = (TimeSpanEditor)d;
			
			ctl.UpdateValue();
		}

		public int Hours
		{
			get { return (int)GetValue(HoursProperty); }
			set { SetValue(HoursProperty, value); }
		}

		// Using a AvaloniaProperty as the backing store for Hours.  This enables animation, styling, binding, etc...
		public static readonly StyledProperty<int> HoursProperty =
			AvaloniaProperty.Register<TimeSpanEditor, int>("Hours", 0);

		private static void OnHoursPropertyChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
		{
			var ctl = (TimeSpanEditor) d;
			if (ctl.Hours > 23)
			{
				ctl.Days++;
				ctl.Hours = 0;
			}
			else if (ctl.Hours < 0)
			{
				ctl.Days--;
				ctl.Hours = 23;
			}

			ctl.UpdateValue();
		}

		public int Minutes
		{
			get { return (int)GetValue(MinutesProperty); }
			set { SetValue(MinutesProperty, value); }
		}

		// Using a AvaloniaProperty as the backing store for Minutes.  This enables animation, styling, binding, etc...
		public static readonly StyledProperty<int> MinutesProperty =
			AvaloniaProperty.Register<TimeSpanEditor, int>("Minutes", 0);

		private static void OnMinutesPropertyChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
		{
			var ctl = (TimeSpanEditor)d;
			if (ctl.Minutes > 59)
			{
				ctl.Hours++;
				ctl.Minutes = 0;
			}
			else if (ctl.Minutes < 0)
			{
				ctl.Hours--;
				ctl.Minutes = 59;
			}

			ctl.UpdateValue();
		}
		 
		public int Seconds
		{
			get { return (int)GetValue(SecondsProperty); }
			set { SetValue(SecondsProperty, value); }
		}

		// Using a AvaloniaProperty as the backing store for Seconds.  This enables animation, styling, binding, etc...
		public static readonly StyledProperty<int> SecondsProperty =
			AvaloniaProperty.Register<TimeSpanEditor, int>("Seconds", 0);

		private static void OnSecondsPropertyChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
		{
			var ctl = (TimeSpanEditor)d;
			if (ctl.Seconds > 59)
			{
				ctl.Minutes++;
				ctl.Seconds = 0;
			}
			else if (ctl.Seconds < 0)
			{
				ctl.Minutes--;
				ctl.Seconds = 59;
			}

			ctl.UpdateValue();
		}
		public int MiliSeconds
		{
			get { return (int)GetValue(MiliSecondsProperty); }
			set { SetValue(MiliSecondsProperty, value); }
		}

		// Using a AvaloniaProperty as the backing store for MiliSeconds.  This enables animation, styling, binding, etc...
		public static readonly StyledProperty<int> MiliSecondsProperty =
			AvaloniaProperty.Register<TimeSpanEditor, int>("MiliSeconds", 0);

		private static void OnMiliSecondsPropertyChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
		{
			var ctl = (TimeSpanEditor)d;
			if (ctl.MiliSeconds > 999)
			{
				ctl.Seconds++;
				ctl.MiliSeconds = 0;
			}
			else if (ctl.MiliSeconds < 0)
			{
				ctl.Seconds--;
				ctl.MiliSeconds = 999;
			}

			ctl.UpdateValue();
		}
	}
}