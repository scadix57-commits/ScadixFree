 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Controls;

namespace Scadix.AxamlDesigner.Services
{
	sealed class DefaultErrorService : IErrorService
	{
		sealed class AttachedErrorBalloon : ErrorBalloon
		{
			Control attachTo;
			
			public AttachedErrorBalloon(Control attachTo, Control errorElement)
			{
				this.attachTo = attachTo;
				this.Content = errorElement;
			}
			
			internal void AttachEvents()
			{
				attachTo.DetachedFromVisualTree += OnCloseEvent;
				attachTo.KeyDown += OnCloseEvent;
				attachTo.PointerPressed += OnCloseEvent;
				attachTo.LostFocus += OnCloseEvent;
			}
			
			void OnCloseEvent(object sender, EventArgs e)
			{
				this.Close();
			}
			
			protected override void OnClosing(WindowClosingEventArgs e)
			{
				attachTo.DetachedFromVisualTree -= OnCloseEvent;
				attachTo.KeyDown -= OnCloseEvent;
				attachTo.PointerPressed -= OnCloseEvent;
				attachTo.LostFocus -= OnCloseEvent;
				base.OnClosing(e);
			}
			
			protected override void OnPointerPressed(PointerPressedEventArgs e)
			{
				base.OnPointerPressed(e);
				Close();
			}
		}
		
		ServiceContainer services;
		
		public DefaultErrorService(DesignContext context)
		{
			this.services = context.Services;
		}
		
		public void ShowErrorTooltip(Control attachTo, Control errorElement)
		{
			if (attachTo == null)
				throw new ArgumentNullException("attachTo");
			if (errorElement == null)
				throw new ArgumentNullException("errorElement");
			
			AttachedErrorBalloon b = new AttachedErrorBalloon(attachTo, errorElement);
			var screenPos = attachTo.PointToScreen(new Avalonia.Point(0, attachTo.Bounds.Height));
			b.Position = new Avalonia.PixelPoint((int)screenPos.X, (int)screenPos.Y - 8);
			// CanActivate not available in Avalonia - window will be shown normally
			ITopLevelWindowService windowService = services.GetService<ITopLevelWindowService>();
			ITopLevelWindow ownerWindow = (windowService != null) ? windowService.GetTopLevelWindow(attachTo) : null;
			if (ownerWindow != null) {
				ownerWindow.SetOwner(b);
			}
			b.Show();
			
			if (ownerWindow != null) {
				ownerWindow.Activate();
			}
			
			b.AttachEvents();
		}
	}
}
