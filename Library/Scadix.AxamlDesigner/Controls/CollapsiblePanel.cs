

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// Allows animated collapsing of the content of this panel.
	/// </summary>
	public class CollapsiblePanel : ContentControl
	{
		protected override Type StyleKeyOverride => typeof(CollapsiblePanel);

		static CollapsiblePanel()
		{
			FocusableProperty.OverrideDefaultValue<CollapsiblePanel>(false);
			IsCollapsedProperty.Changed.AddClassHandler<CollapsiblePanel>(OnIsCollapsedChanged);

        }
		
		public static readonly StyledProperty<bool> IsCollapsedProperty =
			AvaloniaProperty.Register<CollapsiblePanel, bool>("IsCollapsed", false);
		
		public bool IsCollapsed {
			get { return GetValue(IsCollapsedProperty); }
			set { SetValue(IsCollapsedProperty, value); }
		}
		
		public static readonly StyledProperty<Orientation> CollapseOrientationProperty =
			AvaloniaProperty.Register<CollapsiblePanel, Orientation>("CollapseOrientation", Orientation.Vertical);
		
		public Orientation CollapseOrientation {
			get { return GetValue(CollapseOrientationProperty); }
			set { SetValue(CollapseOrientationProperty, value); }
		}
		
		public static readonly StyledProperty<TimeSpan> DurationProperty =
			AvaloniaProperty.Register<CollapsiblePanel, TimeSpan>("Duration", TimeSpan.FromMilliseconds(250));
		
		/// <summary>
		/// The duration in milliseconds of the animation.
		/// </summary>
		public TimeSpan Duration {
			get { return GetValue(DurationProperty); }
			set { SetValue(DurationProperty, value); }
		}
		
		protected internal static readonly StyledProperty<double> AnimationProgressProperty =
			AvaloniaProperty.Register<CollapsiblePanel, double>("AnimationProgress", 1.0);
		
		/// <summary>
		/// Value between 0 and 1 specifying how far the animation currently is.
		/// </summary>
		protected internal double AnimationProgress {
			get { return GetValue(AnimationProgressProperty); }
			set { SetValue(AnimationProgressProperty, value); }
		}
		
		protected internal static readonly StyledProperty<double> AnimationProgressXProperty =
			AvaloniaProperty.Register<CollapsiblePanel, double>("AnimationProgressX", 1.0);
		
		/// <summary>
		/// Value between 0 and 1 specifying how far the animation currently is.
		/// </summary>
		protected internal double AnimationProgressX {
			get { return GetValue(AnimationProgressXProperty); }
			set { SetValue(AnimationProgressXProperty, value); }
		}
		
		protected internal static readonly StyledProperty<double> AnimationProgressYProperty =
			AvaloniaProperty.Register<CollapsiblePanel, double>("AnimationProgressY", 1.0);
		
		/// <summary>
		/// Value between 0 and 1 specifying how far the animation currently is.
		/// </summary>
		protected internal double AnimationProgressY {
			get { return GetValue(AnimationProgressYProperty); }
			set { SetValue(AnimationProgressYProperty, value); }
		}
        static void OnIsCollapsedChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            ((CollapsiblePanel)d).SetupAnimation((bool)e.NewValue);
        }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == IsCollapsedProperty)
				SetupAnimation((bool)change.NewValue!);
		}
		
		void SetupAnimation(bool isCollapsed)
		{
            if (IsLoaded)
            {
                // If the animation is already running, calculate remaining portion of the time
                var currentProgress = AnimationProgress;
                if (!isCollapsed) currentProgress = 1.0 - currentProgress;

                var animation = new DoubleTransition
                {
                    Property = AnimationProgressProperty,
                    Duration = TimeSpan.FromSeconds(Duration.TotalSeconds * currentProgress)
                };

                var transitions = new Transitions { animation };

                if (CollapseOrientation == Orientation.Horizontal)
                {
                    var animationX = new DoubleTransition
                    {
                        Property = AnimationProgressXProperty,
                        Duration = TimeSpan.FromSeconds(Duration.TotalSeconds * currentProgress)
                    };
                    transitions.Add(animationX);
                    AnimationProgressY = 1.0;
                }
                else
                {
                    AnimationProgressX = 1.0;
                    var animationY = new DoubleTransition
                    {
                        Property = AnimationProgressYProperty,
                        Duration = TimeSpan.FromSeconds(Duration.TotalSeconds * currentProgress)
                    };
                    transitions.Add(animationY);
                }

                Transitions = transitions;
                AnimationProgress = isCollapsed ? 0.0 : 1.0;
            }
            else
            {
                AnimationProgress = isCollapsed ? 0.0 : 1.0;
                AnimationProgressX = CollapseOrientation == Orientation.Horizontal ? AnimationProgress : 1.0;
                AnimationProgressY = CollapseOrientation == Orientation.Vertical ? AnimationProgress : 1.0;
            }
        }
	}
	
	sealed class CollapsiblePanelProgressToVisibilityConverter : IValueConverter
	{
		public static readonly CollapsiblePanelProgressToVisibilityConverter Instance = new CollapsiblePanelProgressToVisibilityConverter();
		public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
		{
			if (value is double d)
                return (double)value > 0 ? true : false;
            else
				return true;
		}
		
		public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
	
	public class SelfCollapsingPanel : CollapsiblePanel
	{


        static SelfCollapsingPanel()
        {
			CanCollapseProperty.Changed.AddClassHandler<SelfCollapsingPanel>(OnCanCollapseChanged);


        }




        public static readonly StyledProperty<bool> CanCollapseProperty =
			AvaloniaProperty.Register<SelfCollapsingPanel, bool>("CanCollapse", SharedInstances.BoxedFalse);
		
		public bool CanCollapse {
			get { return GetValue(CanCollapseProperty); }
			set { SetValue(CanCollapseProperty, value); }
		}

        static void OnCanCollapseChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            SelfCollapsingPanel panel = (SelfCollapsingPanel)d;
            if ((bool)e.NewValue)
            {
                if (!panel.HeldOpenByMouse)
                    panel.IsCollapsed = true;
            }
            else
            {
                panel.IsCollapsed = false;
            }
        }

        
		bool HeldOpenByMouse {
			get { return IsPointerOver; }
		}
		
		protected override void OnPointerExited(PointerEventArgs e)
		{
			base.OnPointerExited(e);
			if (CanCollapse && !HeldOpenByMouse)
				IsCollapsed = true;
		}
		
		protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
		{
			base.OnPointerCaptureLost(e);
			if (CanCollapse && !HeldOpenByMouse)
				IsCollapsed = true;
		}
	}
}
