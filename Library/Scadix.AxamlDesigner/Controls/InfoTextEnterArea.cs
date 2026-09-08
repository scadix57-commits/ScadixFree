

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using System.Diagnostics;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesigner.Services;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// A Info text area.
	/// </summary>
	public sealed class InfoTextEnterArea : TemplatedControl
    {
        protected override Type StyleKeyOverride => typeof(InfoTextEnterArea);

        Geometry activeAreaGeometry;
		AdornerPanel adornerPanel;
		IDesignPanel designPanel;
		
		public InfoTextEnterArea()
		{
			this.IsHitTestVisible = false;
		}		
			
		public Geometry ActiveAreaGeometry {
			get { return activeAreaGeometry; }
			set {
				activeAreaGeometry = value;
			}
		}	
		
		public static void Start(ref InfoTextEnterArea grayOut, ServiceContainer services, Control activeContainer, string text)
		{
			Debug.Assert(activeContainer != null);
			Start(ref grayOut, services, activeContainer, new Rect(activeContainer.Bounds.Size), text);
		}
		
		public static void Start(ref InfoTextEnterArea grayOut, ServiceContainer services, Control activeContainer, Rect activeRectInActiveContainer, string text)
		{
			Debug.Assert(services != null);
			Debug.Assert(activeContainer != null);
			DesignPanel designPanel = services.GetService<IDesignPanel>() as DesignPanel;
			OptionService optionService = services.GetService<OptionService>();
			if (designPanel != null && grayOut == null && optionService != null && optionService.GrayOutDesignSurfaceExceptParentContainerWhenDragging) {
				grayOut = new InfoTextEnterArea();
				grayOut.designPanel = designPanel;
				grayOut.adornerPanel = new AdornerPanel();
				grayOut.adornerPanel.Order = AdornerOrder.Background;
				grayOut.adornerPanel.SetAdornedElement(designPanel.Context.RootItem.View, null);
				var transform = activeContainer.TransformToVisual(grayOut.adornerPanel.AdornedElement);
				grayOut.ActiveAreaGeometry = new RectangleGeometry(activeRectInActiveContainer);
				var tb = new TextBlock(){Text = text};
				tb.FontSize = 10;
				tb.ClipToBounds = true;
				tb.Width = activeContainer.Bounds.Width;
				tb.Height = activeContainer.Bounds.Height;
				tb.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
				tb.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
				if (transform.HasValue)
					tb.RenderTransform = new Avalonia.Media.MatrixTransform(transform.Value);
				grayOut.adornerPanel.Children.Add(tb);
								
				designPanel.Adorners.Add(grayOut.adornerPanel);
			}
		}
																		 
		public static void Stop(ref InfoTextEnterArea grayOut)
		{
			if (grayOut != null) {
				IDesignPanel designPanel = grayOut.designPanel;
				AdornerPanel adornerPanelToRemove = grayOut.adornerPanel;
				designPanel.Adorners.Remove(adornerPanelToRemove);								
				grayOut = null;
			}
		}
	}
}
