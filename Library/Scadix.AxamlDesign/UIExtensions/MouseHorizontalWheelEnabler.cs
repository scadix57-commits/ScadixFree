

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Xml.Linq;

namespace Scadix.AxamlDesign.UIExtensions
{
	public  class MouseHorizontalWheelEnabler
	{
		/// <summary>
		///   When true it will try to enable Horizontal Wheel support on parent windows/popups/context menus automatically
		///   so the programmer does not need to call it.
		///   Defaults to true.
		/// </summary>
		public static bool AutoEnableMouseHorizontalWheelSupport = true;

		private static readonly HashSet<IntPtr> _HookedWindows = new HashSet<IntPtr>();

		/// <summary>
		///   Enable Horizontal Wheel support for all the controls inside the window.
		///   This method does not need to be called if AutoEnableMouseHorizontalWheelSupport is true.
		///   This does not include popups or context menus.
		///   If it was already enabled it will do nothing.
		/// </summary>
		/// <param name="window">Window to enable support for.</param>
		public static void EnableMouseHorizontalWheelSupport(Window window)
		{
			if (window == null)
			{
				throw new ArgumentNullException(nameof(window));
			}

			if (window.IsLoaded)
			{
                // handle should be available at this level
                IntPtr handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                EnableMouseHorizontalWheelSupport(handle);
			}
			else
			{
				window.Loaded += (sender, args) => {
                    IntPtr handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                    EnableMouseHorizontalWheelSupport(handle);
				};
			}
		}

		/// <summary>
		///   Enable Horizontal Wheel support for all the controls inside the popup.
		///   This method does not need to be called if AutoEnableMouseHorizontalWheelSupport is true.
		///   This does not include sub-popups or context menus.
		///   If it was already enabled it will do nothing.
		/// </summary>
		/// <param name="popup">Popup to enable support for.</param>
		public static void EnableMouseHorizontalWheelSupport(Popup popup)
		{
			if (popup == null)
			{
				throw new ArgumentNullException(nameof(popup));
			}

			if (popup.IsOpen)
			{
				// handle should be available at this level
				// ReSharper disable once PossibleInvalidOperationException
				EnableMouseHorizontalWheelSupport(GetObjectParentHandle(popup.Child).Value);
			}

			// also hook for IsOpened since a new window is created each time
			popup.Opened += (sender, args) => {
				// ReSharper disable once PossibleInvalidOperationException
				EnableMouseHorizontalWheelSupport(GetObjectParentHandle(popup.Child).Value);
			};
		}

		/// <summary>
		///   Enable Horizontal Wheel support for all the controls inside the context menu.
		///   This method does not need to be called if AutoEnableMouseHorizontalWheelSupport is true.
		///   This does not include popups or sub-context menus.
		///   If it was already enabled it will do nothing.
		/// </summary>
		/// <param name="contextMenu">Context menu to enable support for.</param>
		public static void EnableMouseHorizontalWheelSupport(ContextMenu contextMenu)
		{
			if (contextMenu == null)
			{
				throw new ArgumentNullException(nameof(contextMenu));
			}

			if (contextMenu.IsOpen)
			{
				// handle should be available at this level
				// ReSharper disable once PossibleInvalidOperationException
				EnableMouseHorizontalWheelSupport(GetObjectParentHandle(contextMenu).Value);
			}

			// also hook for IsOpened since a new window is created each time
			contextMenu.Opened += (sender, args) => {
				// ReSharper disable once PossibleInvalidOperationException
				EnableMouseHorizontalWheelSupport(GetObjectParentHandle(contextMenu).Value);
			};
		}

		private static IntPtr? GetObjectParentHandle(AvaloniaObject depObj)
		{
			if (depObj == null)
			{
				throw new ArgumentNullException(nameof(depObj));
			}

			var presentationSource = TopLevel.GetTopLevel(depObj as Visual);
            return presentationSource?.TryGetPlatformHandle()?.Handle;
		}

		/// <summary>
		///   Enable Horizontal Wheel support for all the controls inside the HWND.
		///   This method does not need to be called if AutoEnableMouseHorizontalWheelSupport is true.
		///   This does not include popups or sub-context menus.
		///   If it was already enabled it will do nothing.
		/// </summary>
		/// <param name="handle">HWND handle to enable support for.</param>
		/// <returns>True if it was enabled or already enabled, false if it couldn't be enabled.</returns>
		public static bool EnableMouseHorizontalWheelSupport(IntPtr handle)
		{
			if (_HookedWindows.Contains(handle))
			{
				return true;
			}

			_HookedWindows.Add(handle);
            HwndSourceWrapper source = HwndSourceWrapper.FromHwnd(handle);
			if (source == null)
			{
				return false;
			}

			source.AddHook(WndProcHook);
			return true;
		}

		/// <summary>
		///   Disable Horizontal Wheel support for all the controls inside the HWND.
		///   This method does not need to be called in most cases.
		///   This does not include popups or sub-context menus.
		///   If it was already disabled it will do nothing.
		/// </summary>
		/// <param name="handle">HWND handle to disable support for.</param>
		/// <returns>True if it was disabled or already disabled, false if it couldn't be disabled.</returns>
		public static bool DisableMouseHorizontalWheelSupport(IntPtr handle)
		{
			if (!_HookedWindows.Contains(handle))
			{
				return true;
			}

            HwndSourceWrapper source = HwndSourceWrapper.FromHwnd(handle);
			if (source == null)
			{
				return false;
			}

			source.RemoveHook(WndProcHook);
			_HookedWindows.Remove(handle);
			return true;
		}

		/// <summary>
		///   Disable Horizontal Wheel support for all the controls inside the window.
		///   This method does not need to be called in most cases.
		///   This does not include popups or sub-context menus.
		///   If it was already disabled it will do nothing.
		/// </summary>
		/// <param name="window">Window to disable support for.</param>
		/// <returns>True if it was disabled or already disabled, false if it couldn't be disabled.</returns>
		public static bool DisableMouseHorizontalWheelSupport(Window window)
		{
			if (window == null)
			{
				throw new ArgumentNullException(nameof(window));
			}

            IntPtr handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            return DisableMouseHorizontalWheelSupport(handle);
		}

		/// <summary>
		///   Disable Horizontal Wheel support for all the controls inside the popup.
		///   This method does not need to be called in most cases.
		///   This does not include popups or sub-context menus.
		///   If it was already disabled it will do nothing.
		/// </summary>
		/// <param name="popup">Popup to disable support for.</param>
		/// <returns>True if it was disabled or already disabled, false if it couldn't be disabled.</returns>
		public static bool DisableMouseHorizontalWheelSupport(Popup popup)
		{
			if (popup == null)
			{
				throw new ArgumentNullException(nameof(popup));
			}

			IntPtr? handle = GetObjectParentHandle(popup.Child);
			if (handle == null)
			{
				return false;
			}

			return DisableMouseHorizontalWheelSupport(handle.Value);
		}

		/// <summary>
		///   Disable Horizontal Wheel support for all the controls inside the context menu.
		///   This method does not need to be called in most cases.
		///   This does not include popups or sub-context menus.
		///   If it was already disabled it will do nothing.
		/// </summary>
		/// <param name="contextMenu">Context menu to disable support for.</param>
		/// <returns>True if it was disabled or already disabled, false if it couldn't be disabled.</returns>
		public static bool DisableMouseHorizontalWheelSupport(ContextMenu contextMenu)
		{
			if (contextMenu == null)
			{
				throw new ArgumentNullException(nameof(contextMenu));
			}

			IntPtr? handle = GetObjectParentHandle(contextMenu);
			if (handle == null)
			{
				return false;
			}

			return DisableMouseHorizontalWheelSupport(handle.Value);
		}


		/// <summary>
		///   Enable Horizontal Wheel support for all that control and all controls hosted by the same window/popup/context menu.
		///   This method does not need to be called if AutoEnableMouseHorizontalWheelSupport is true.
		///   If it was already enabled it will do nothing.
		/// </summary>
		/// <param name="control">UI Element to enable support for.</param>
		public static void EnableMouseHorizontalWheelSupportForParentOf(Control control)
		{
			// try to add it right now
			if (control is Window)
			{
				EnableMouseHorizontalWheelSupport((Window)control);
			}
			else if (control is Popup)
			{
				EnableMouseHorizontalWheelSupport((Popup)control);
			}
			else if (control is ContextMenu)
			{
				EnableMouseHorizontalWheelSupport((ContextMenu)control);
			}
			else
			{
				IntPtr? parentHandle = GetObjectParentHandle(control as AvaloniaObject);
				if (parentHandle != null)
				{
					EnableMouseHorizontalWheelSupport(parentHandle.Value);
				}

               
                // and in the rare case the parent window ever changes...
                if (control is Control Control)
                {
                    control.AttachedToVisualTree += PresenationSourceChangedHandler;
                }
            }
		}

        private static void PresenationSourceChangedHandler(object sender, VisualTreeAttachmentEventArgs sourceChangedEventArgs)
        {
            var src = TopLevel.GetTopLevel(sourceChangedEventArgs.Parent);
            if (src != null)
            {
                var handle = src.TryGetPlatformHandle()?.Handle;
                if (handle.HasValue)
                {
                    EnableMouseHorizontalWheelSupport(handle.Value);
                }
            }
        }

        private static void HandleMouseHorizontalWheel(IntPtr wParam)
		{

			int tilt = Win32.HiWord(wParam);
			if (tilt == 0)
			{
				return;
			}

			IInputElement element = null;
			if (element == null)
			{
				return;
			}

			if (!(element is Control))
			{
				element = UIHelpers.FindAncestor<Control>(element as AvaloniaObject);
			}
			if (element == null)
			{
				return;
			}

			var ev = new MouseHorizontalWheelEventArgs(null, Environment.TickCount, tilt)
			{
				RoutedEvent = PreviewMouseHorizontalWheelEvent
				//Source = handledWindow
			};

			// first raise preview
			element.RaiseEvent(ev);
			if (ev.Handled)
			{
				return;
			}

			// then bubble it
			ev.RoutedEvent = MouseHorizontalWheelEvent;
			element.RaiseEvent(ev);
		}

		private static IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			// transform horizontal mouse wheel messages 
			switch (msg)
			{
				case Win32.WM_MOUSEHWHEEL:
					HandleMouseHorizontalWheel(wParam);
					break;
			}
			return IntPtr.Zero;
		}

		private static class Win32
		{
			// ReSharper disable InconsistentNaming
			public const int WM_MOUSEHWHEEL = 0x020E;
			// ReSharper restore InconsistentNaming

			public static int GetIntUnchecked(IntPtr value)
			{
				return IntPtr.Size == 8 ? unchecked((int)value.ToInt64()) : value.ToInt32();
			}

			public static int HiWord(IntPtr ptr)
			{
				return unchecked((short)((uint)GetIntUnchecked(ptr) >> 16));
			}
		}

		#region MouseWheelHorizontal Event

		public static readonly RoutedEvent<RoutedEventArgs> MouseHorizontalWheelEvent =
          RoutedEvent.Register<MouseHorizontalWheelEnabler, RoutedEventArgs>("MouseHorizontalWheel", RoutingStrategies.Bubble);


        public static void AddMouseHorizontalWheelHandler(AvaloniaObject d, EventHandler<RoutedEventArgs> handler)
		{
			var uie = d as Control;
			if (uie != null)
			{
				uie.AddHandler(MouseHorizontalWheelEvent, handler);

				if (AutoEnableMouseHorizontalWheelSupport)
				{
					EnableMouseHorizontalWheelSupportForParentOf(uie);
				}
			}
		}

		public static void RemoveMouseHorizontalWheelHandler(AvaloniaObject d, EventHandler<RoutedEventArgs> handler)
		{
			var uie = d as Control;
			uie?.RemoveHandler(MouseHorizontalWheelEvent, handler);
		}

        #endregion

        #region PreviewMouseWheelHorizontal Event

        public static readonly RoutedEvent<RoutedEventArgs> PreviewMouseHorizontalWheelEvent =
          RoutedEvent.Register<MouseHorizontalWheelEnabler, RoutedEventArgs>("PreviewMouseHorizontalWheel", RoutingStrategies.Tunnel);

        public static void AddPreviewMouseHorizontalWheelHandler(AvaloniaObject d, EventHandler<RoutedEventArgs> handler)
		{
			var uie = d as Control;
			if (uie != null)
			{
				uie.AddHandler(PreviewMouseHorizontalWheelEvent, handler);

				if (AutoEnableMouseHorizontalWheelSupport)
				{
					EnableMouseHorizontalWheelSupportForParentOf(uie);
				}
			}
		}

		public static void RemovePreviewMouseHorizontalWheelHandler(AvaloniaObject d, EventHandler<RoutedEventArgs> handler)
		{
			var uie = d as Control;
			uie?.RemoveHandler(PreviewMouseHorizontalWheelEvent, handler);
		}

        #endregion
        // Dummy classes added specifically to preserve your required WPF structures (HwndSource & Custom EventArgs) in Avalonia
        internal class HwndSourceWrapper
        {
            public delegate IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);
            public static HwndSourceWrapper FromHwnd(IntPtr handle) => new HwndSourceWrapper();
            public void AddHook(WndProc hook) { }
            public void RemoveHook(WndProc hook) { }
        }

       
    }
}
