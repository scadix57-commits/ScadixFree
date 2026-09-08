

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Diagnostics;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesigner.Services;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// Gray out everything except a specific area.
	/// </summary>
	public sealed class GrayOutDesignerExceptActiveArea : Control
	{
		Geometry? designSurfaceRectangle;
		Geometry? activeAreaGeometry;
		Geometry? combinedGeometry;
		Brush? grayOutBrush;
		AdornerPanel? adornerPanel;
		IDesignPanel? designPanel;
		Control? activeContainer;
		const double MaxOpacity = 0.3;
		
		public GrayOutDesignerExceptActiveArea()
		{
			this.GrayOutBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));
			this.GrayOutBrush.Opacity = MaxOpacity;
			this.IsHitTestVisible = false;
		}
		
		public Brush? GrayOutBrush {
			get { return grayOutBrush; }
			set { grayOutBrush = value; }
		}
		
		public Geometry? ActiveAreaGeometry {
			get { return activeAreaGeometry; }
			set {
				activeAreaGeometry = value;
				if (designSurfaceRectangle != null && activeAreaGeometry != null)
					combinedGeometry = new CombinedGeometry(GeometryCombineMode.Exclude, designSurfaceRectangle, activeAreaGeometry);
			}
		}
		
		public override void Render(DrawingContext drawingContext)
		{
			if (grayOutBrush != null && combinedGeometry != null)
				drawingContext.DrawGeometry(grayOutBrush, null, combinedGeometry);
		}
		
		Rect currentAnimateActiveAreaRectToTarget;
		
		public void AnimateActiveAreaRectTo(Rect newRect)
		{
			if (newRect.Equals(currentAnimateActiveAreaRectToTarget))
				return;
			// Transform rect from activeContainer coords to adornerPanel coords
			Rect transformedRect = newRect;
			if (activeContainer != null && adornerPanel?.AdornedElement != null) {
				var transform = activeContainer.TransformToVisual(adornerPanel.AdornedElement);
				if (transform.HasValue) {
					var tl = transform.Value.Transform(newRect.TopLeft);
					var br = transform.Value.Transform(newRect.BottomRight);
					transformedRect = new Rect(tl, br);
				}
			}
			if (activeAreaGeometry is RectangleGeometry rg)
				rg.Rect = transformedRect;
			currentAnimateActiveAreaRectToTarget = newRect;
			InvalidateVisual();
		}
		
		public static void Start(ref GrayOutDesignerExceptActiveArea? grayOut, ServiceContainer services, Control activeContainer)
		{
			Debug.Assert(activeContainer != null);
			Start(ref grayOut, services, activeContainer, new Rect(activeContainer.Bounds.Size));
		}
		
		public static void Start(ref GrayOutDesignerExceptActiveArea? grayOut, ServiceContainer services, Control activeContainer, Rect activeRectInActiveContainer)
		{
			Debug.Assert(services != null);
			Debug.Assert(activeContainer != null);
			DesignPanel? designPanel = services.GetService<IDesignPanel>() as DesignPanel;
			OptionService? optionService = services.GetService<OptionService>();
			if (designPanel != null && grayOut == null && optionService != null && optionService.GrayOutDesignSurfaceExceptParentContainerWhenDragging) {
				grayOut = new GrayOutDesignerExceptActiveArea();
				var child = designPanel.Child as Border;
				var innerChild = child?.Child as Control;
				var size = innerChild?.Bounds.Size ?? new Size(0, 0);
				grayOut.designSurfaceRectangle = new RectangleGeometry(new Rect(0, 0, size.Width, size.Height));
				grayOut.designPanel = designPanel;
				grayOut.adornerPanel = new AdornerPanel();
				grayOut.adornerPanel.Order = AdornerOrder.BehindForeground;
				grayOut.adornerPanel.SetAdornedElement(designPanel.Context.RootItem.View, null);
				grayOut.adornerPanel.Children.Add(grayOut);
				grayOut.activeContainer = activeContainer;
				// Transform activeRectInActiveContainer from activeContainer coords to adornerPanel coords
				var transform = activeContainer.TransformToVisual(grayOut.adornerPanel.AdornedElement);
				Rect transformedRect = activeRectInActiveContainer;
				if (transform.HasValue) {
					var tl = transform.Value.Transform(activeRectInActiveContainer.TopLeft);
					var br = transform.Value.Transform(activeRectInActiveContainer.BottomRight);
					transformedRect = new Rect(tl, br);
				}
				grayOut.ActiveAreaGeometry = new RectangleGeometry(transformedRect);
				grayOut.GrayOutBrush!.Opacity = MaxOpacity;
				designPanel.Adorners.Add(grayOut.adornerPanel);
			}
		}
		
		static readonly TimeSpan animationTime = new TimeSpan(2000000);

        static void Animate(AvaloniaObject element, AvaloniaProperty property, double to)
        {
            // �� ������ǡ ������ ������ �� ��� KeyFrames ������� �������
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(200), // ��� ������ (animationTime)
                FillMode = FillMode.Forward, // ����� FillBehavior.HoldEnd
                Children =
        {
            new KeyFrame
            {
                Cue = new Cue(1.0), // ������� (100%)
                Setters = { new Setter(property, to) }
            }
        }
            };

            // ������� (����� BeginAnimation)
            animation.RunAsync((Animatable)element);
        }


        public static void Stop(ref GrayOutDesignerExceptActiveArea? grayOut)
		{
			if (grayOut != null) {
                Animate(grayOut.GrayOutBrush, Brush.OpacityProperty, 0);
                IDesignPanel? dp = grayOut.designPanel;
				AdornerPanel? adornerPanelToRemove = grayOut.adornerPanel;
				DispatcherTimer timer = new DispatcherTimer();
				timer.Interval = animationTime;
				timer.Tick += delegate {
					timer.Stop();
					dp?.Adorners.Remove(adornerPanelToRemove!);
				};
				timer.Start();
				grayOut = null;
			}
		}
	}
}
