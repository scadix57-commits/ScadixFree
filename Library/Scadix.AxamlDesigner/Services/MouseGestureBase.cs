 

using Avalonia;
using Avalonia.Input;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.Services
{
	/// <summary>
	/// Base class for classes handling mouse gestures on the design surface.
	/// </summary>
	public abstract class MouseGestureBase
	{
		/// <summary>
		/// Checks if <paramref name="button"/> is the only button that is currently pressed.
		/// </summary>
		public static bool IsOnlyButtonPressed(PointerPressedEventArgs e, MouseButton button)
		{
			var props = e.GetCurrentPoint(null).Properties;
			return button == MouseButton.Left && props.IsLeftButtonPressed
				|| button == MouseButton.Middle && props.IsMiddleButtonPressed
				|| button == MouseButton.Right && props.IsRightButtonPressed;
		}
		
		protected IDesignPanel designPanel;
		protected ServiceContainer services;
		protected bool canAbortWithEscape = true;
		bool isStarted;
		IPointer capturedPointer;
		
		public void Start(IDesignPanel designPanel, PointerPressedEventArgs e)
		{
			if (designPanel == null)
				throw new ArgumentNullException("designPanel");
			if (e == null)
				throw new ArgumentNullException("e");
			if (isStarted)
				throw new InvalidOperationException("Gesture already was started");
			
			isStarted = true;
			this.designPanel = designPanel;
			this.services = designPanel.Context.Services;
			
			var panel = designPanel as Avalonia.Controls.Control;
			if (panel != null) {
				e.Pointer.Capture(panel);
				capturedPointer = e.Pointer;
				RegisterEvents();
				OnStarted(e);
			} else {
				Stop();
			}
		}
		
		void RegisterEvents()
		{
			var panel = designPanel as Avalonia.Controls.Control;
			if (panel == null) return;
			panel.PointerCaptureLost += OnPointerCaptureLost;
			panel.PointerPressed += OnPointerPressed;
			panel.PointerMoved += OnPointerMoved;
			panel.PointerReleased += OnPointerReleased;
			panel.KeyDown += OnKeyDown;
		}
		
		void UnRegisterEvents()
		{
			var panel = designPanel as Avalonia.Controls.Control;
			if (panel == null) return;
			panel.PointerCaptureLost -= OnPointerCaptureLost;
			panel.PointerPressed -= OnPointerPressed;
			panel.PointerMoved -= OnPointerMoved;
			panel.PointerReleased -= OnPointerReleased;
			panel.KeyDown -= OnKeyDown;
		}
		
		void OnKeyDown(object sender, KeyEventArgs e)
		{
			if (canAbortWithEscape && e.Key == Key.Escape) {
				e.Handled = true;
				Stop();
			}
		}
		
		void OnPointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
		{
			Stop();
		}
		
		protected virtual void OnPointerPressed(object sender, PointerPressedEventArgs e)
		{
			if (MouseButtonHelper.IsDoubleClick(sender, e))
				OnMouseDoubleClick(sender, e);
		}
		
		protected virtual void OnPointerMoved(object sender, PointerEventArgs e)
		{
			OnMouseMove(sender, e);
		}
		
		protected virtual void OnPointerReleased(object sender, PointerReleasedEventArgs e)
		{
			OnMouseUp(sender, e);
		}

		protected virtual void OnMouseDoubleClick(object sender, PointerPressedEventArgs e)
		{ }
		
		protected virtual void OnMouseDown(object sender, PointerPressedEventArgs e)
		{ }
		
		protected virtual void OnMouseMove(object sender, PointerEventArgs e)
		{
		}
		
		protected virtual void OnMouseUp(object sender, PointerReleasedEventArgs e)
		{
			Stop();
		}
		
		protected void Stop()
		{
			if (!isStarted) return;
			isStarted = false;
			capturedPointer?.Capture(null);
			capturedPointer = null;
			UnRegisterEvents();
			OnStopped();
		}
		
		protected virtual void OnStarted(PointerPressedEventArgs e) {}
		protected virtual void OnStopped() {}
		
		static class MouseButtonHelper
		{
			private static readonly uint k_DoubleClickSpeed = 500; // default 500ms
			
			private const double k_MaxMoveDistance = 10;
			
			private static long _LastClickTicks = 0;
			private static Point _LastPosition;
			private static WeakReference _LastSender;
			
			internal static bool IsDoubleClick(object sender, PointerPressedEventArgs e)
			{
				Point position = e.GetPosition(null);
				long clickTicks = DateTime.Now.Ticks;
				long elapsedTicks = clickTicks - _LastClickTicks;
				long elapsedTime = elapsedTicks / TimeSpan.TicksPerMillisecond;
				bool quickClick = (elapsedTime <= k_DoubleClickSpeed);
				bool senderMatch = (_LastSender != null && sender.Equals(_LastSender.Target));
				
				if (senderMatch && quickClick && Distance(position, _LastPosition) <= k_MaxMoveDistance)
				{
					// Double click!
					_LastClickTicks = 0;
					_LastSender = null;
					return true;
				}
				
				// Not a double click
				_LastClickTicks = clickTicks;
				_LastPosition = position;
				if (!quickClick)
					_LastSender = new WeakReference(sender);
				return false;
			}
			
			private static double Distance(Point pointA, Point pointB)
			{
				double x = pointA.X - pointB.X;
				double y = pointA.Y - pointB.Y;
				return Math.Sqrt(x * x + y * y);
			}
		}
	}
}
