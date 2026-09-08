
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Reflection;
using System.Resources;


namespace Scadix.AxamlDesigner.Controls
{
	public class ZoomControl : ZoomScrollViewer
	{
		protected override Type StyleKeyOverride => typeof(ZoomControl);

		public ZoomControl()
		{
			PanToolCursor = GetCursor("avares://Scadix.AxamlDesigner/Images/PanToolCursor.cur");
			PanToolCursorMouseDown = GetCursor("avares://Scadix.AxamlDesigner/Images/PanToolCursorMouseDown.cur");
		}

		public object AdditionalControls
		{
			get { return (object)GetValue(AdditionalControlsProperty); }
			set { SetValue(AdditionalControlsProperty, value); }
		}
		
		public static readonly StyledProperty<object> AdditionalControlsProperty =
			AvaloniaProperty.Register<ZoomControl, object>("AdditionalControls",null);

		internal static Cursor GetCursor(string path)
		{
            try
            {

                var bitmap = new Bitmap(AssetLoader.Open(new Uri(path)));
                {
                    if (bitmap != null)
                    {

                        return new Cursor(bitmap, PixelPoint.Origin);
                    }
                }
            }
            catch
            {
                // Fallback to default cursor if resource not found
            }

            return new Cursor(StandardCursorType.Arrow);
        }

		static Cursor PanToolCursor;
		static Cursor PanToolCursorMouseDown;
		
		double startHorizontalOffset;
		double startVericalOffset;
		Point startPoint;
		bool isMouseDown;
		bool pan;

		private bool _spaceKeyDown = false;

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (!pan && e.Key == Key.Space) {
				pan = true;
				_spaceKeyDown = true;
                InvalidateVisual();
                // Mouse.UpdateCursor();
            }
			base.OnKeyDown(e);
		}

		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (e.Key == Key.Space) {
				pan = false;
				_spaceKeyDown = false;
                InvalidateVisual();
                // Mouse.UpdateCursor();
            }
			base.OnKeyUp(e);
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			if (!pan && e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed)
			{
				pan = true;
                InvalidateVisual();
            }
			
			if (pan && !e.Handled) {
				e.Pointer.Capture(this);
				isMouseDown = true;
				e.Handled = true;
				startPoint = e.GetPosition(this);
				PanStart();
				this.Cursor = PanToolCursorMouseDown;
                InvalidateVisual();
            }
			base.OnPointerPressed(e);
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			if (isMouseDown) {
				var endPoint = e.GetPosition(this);
				PanContinue(endPoint - startPoint);
			}
			// Update cursor based on pan state
			if (pan || isMouseDown) {
				this.Cursor = isMouseDown ? PanToolCursorMouseDown : PanToolCursor;
			} else {
				this.Cursor = null;
			}
			base.OnPointerMoved(e);
		}

		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			if (pan && !e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed && !_spaceKeyDown)
			{
				pan = false;
				this.Cursor = null;
                InvalidateVisual();
            }
			
			if (isMouseDown) {
				isMouseDown = false;
				e.Pointer.Capture(null);
				this.Cursor = pan ? PanToolCursor : null;
                InvalidateVisual();
            }
			base.OnPointerReleased(e);
		}
		
		protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
		{
			if (isMouseDown) {
				isMouseDown = false;
                InvalidateVisual();
            }
			base.OnPointerCaptureLost(e);
		}
		
		void PanStart()
		{
			startHorizontalOffset = this.Offset.X;
			startVericalOffset = this.Offset.Y;
		}

		void PanContinue(Vector delta)
		{
			this.Offset = new Vector(startHorizontalOffset - delta.X / this.CurrentZoom, startVericalOffset - delta.Y / this.CurrentZoom);
		}
	}
}
