 

using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Scadix.AxamlDesign.Extensions;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// A custom control that imitates the properties of <see cref="Window"/>, but is not a top-level control.
	/// </summary>
	public class WindowClone : ContentControl
	{
		protected override Type StyleKeyOverride => typeof(WindowClone);


        static WindowClone()
        {
           
            Control.IsTabStopProperty.OverrideMetadata(typeof(WindowClone), new StyledPropertyMetadata<bool>(SharedInstances.BoxedFalse));
           

            //KeyboardNavigation.DirectionalNavigationProperty.OverrideMetadata(typeof(WindowClone), new StyledPropertyMetadata(SharedInstances<KeyboardNavigationMode>.Box(KeyboardNavigationMode.Cycle)));
           
			KeyboardNavigation.TabNavigationProperty.OverrideMetadata<WindowClone>(new StyledPropertyMetadata<KeyboardNavigationMode>((KeyboardNavigationMode) SharedInstances<KeyboardNavigationMode>.Box(KeyboardNavigationMode.Cycle)));
           
			//KeyboardNavigation.ControlTabNavigationProperty.OverrideMetadata(typeof(WindowClone), new FrameworkPropertyMetadata(SharedInstances<KeyboardNavigationMode>.Box(KeyboardNavigationMode.Cycle)));
           
			//FocusManager.IsFocusScopeProperty.OverrideMetadata(typeof(WindowClone), new FrameworkPropertyMetadata(SharedInstances.BoxedTrue));
        }

        /// <summary>
        /// This property has no effect. (for compatibility with <see cref="Window"/> only).
        /// </summary>
        public bool AllowsTransparency { get; set; }

		/// <summary>
		/// This property has no effect. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public bool? DialogResult { get; set; }

		/// <summary>
		/// This property has no effect. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public static readonly StyledProperty<IImage> IconProperty =
			AvaloniaProperty.Register<WindowClone, IImage>("Icon");

		public IImage Icon {
			get { return GetValue(IconProperty); }
			set { SetValue(IconProperty, value); }
		}

		/// <summary>
		/// This property has no effect. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public double Left { get; set; }

		Window owner;

		/// <summary>
		/// This property has no effect. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public Window Owner {
			get { return owner; }
			set { owner = value; }
		}

		/// <summary>
		/// Gets or sets whether the window can be resized (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public bool CanResize { get; set; } = true;

		/// <summary>
		/// This property has no effect. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public bool ShowActivated { get; set; }

		/// <summary>
		/// This property has no effect. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public bool ShowInTaskbar { get; set; }

		/// <summary>
		/// Gets or sets a value that specifies whether a window will automatically size itself to fit the size of its content.
		/// </summary>
		public SizeToContent SizeToContent {
			get { return (SizeToContent)GetValue(Window.SizeToContentProperty); }
			set { SetValue(Window.SizeToContentProperty, value); }
		}

		/// <summary>
		/// The title to display in the Window's title bar.
		/// </summary>
		public static readonly StyledProperty<string> TitleProperty =
			AvaloniaProperty.Register<WindowClone, string>("Title");

		public string Title {
			get { return GetValue(TitleProperty); }
			set { SetValue(TitleProperty, value); }
		}

		/// <summary>
		/// This property has no effect. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public double Top { get; set; }

		/// <summary>
		/// This property has no effect. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public bool Topmost {
			get { return (bool)GetValue(Window.TopmostProperty); }
			set { SetValue(Window.TopmostProperty, SharedInstances.Box(value)); }
		}

		WindowStartupLocation windowStartupLocation;

		/// <summary>
		/// This property has no effect. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public WindowStartupLocation WindowStartupLocation {
			get { return windowStartupLocation; }
			set { windowStartupLocation = value; }
		}

		/// <summary>
		/// This property has no effect. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public WindowState WindowState {
			get { return (WindowState)GetValue(Window.WindowStateProperty); }
			set { SetValue(Window.WindowStateProperty, value); }
		}

		/// <summary>
		/// This event is never raised. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public event EventHandler Activated { add {} remove {} }

		/// <summary>
		/// This event is never raised. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public event EventHandler Closed { add {} remove {} }

		/// <summary>
		/// This event is never raised. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public event EventHandler Closing { add {} remove {} }

		/// <summary>
		/// This event is never raised. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public event EventHandler ContentRendered { add {} remove {} }

		/// <summary>
		/// This event is never raised. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public event EventHandler Deactivated { add {} remove {} }

		/// <summary>
		/// This event is never raised. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public event EventHandler LocationChanged { add {} remove {} }

		/// <summary>
		/// This event is never raised. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public event EventHandler SourceInitialized { add {} remove {} }

		/// <summary>
		/// This event is never raised. (for compatibility with <see cref="Window"/> only).
		/// </summary>
		public event EventHandler StateChanged { add {} remove {} }
	}

	/// <summary>
	/// A <see cref="CustomInstanceFactory"/> for <see cref="Window"/>
	/// (and derived classes, unless they specify their own <see cref="CustomInstanceFactory"/>).
	/// </summary>
	[ExtensionFor(typeof(Window))]
	public class WindowCloneExtension : CustomInstanceFactory
	{
		/// <summary>
		/// Used to create instances of <see cref="WindowClone"/>.
		/// </summary>
		public override object CreateInstance(Type type, params object[] arguments)
		{
			Debug.Assert(arguments.Length == 0);
			return new WindowClone();
		}
	}
}
