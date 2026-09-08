
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Scadix.AxamlDesign.Adorners;

namespace Scadix.AxamlDesigner.Controls.Thumbs
{
	/// <summary>
	/// Description of MultiPointThumb.
	/// </summary>
	public class PointThumb : DesignerThumb
	{
		public Transform InnerRenderTransform
		{
			get { return (Transform)GetValue(InnerRenderTransformProperty); }
			set { SetValue(InnerRenderTransformProperty, value); }
		}

		public static readonly StyledProperty<Transform> InnerRenderTransformProperty =
			AvaloniaProperty.Register<PointThumb, Transform>("InnerRenderTransform");

		public bool IsEllipse
		{
			get { return (bool)GetValue(IsEllipseProperty); }
			set { SetValue(IsEllipseProperty, value); }
		}

		public static readonly StyledProperty<bool> IsEllipseProperty =
			AvaloniaProperty.Register<PointThumb, bool>("IsEllipse", false);

		public Point Point
		{
			get { return (Point)GetValue(PointProperty); }
			set { SetValue(PointProperty, value); }
		}

		public static readonly StyledProperty<Point> PointProperty =
			AvaloniaProperty.Register<PointThumb, Point>("Point");

		static PointThumb()
		{
           

            PointProperty.Changed.AddClassHandler<PointThumb>(OnPointChanged);
            InnerRenderTransformProperty.OverrideMetadata(typeof(PointThumb), new StyledPropertyMetadata<Transform>(null));
            IsEllipseProperty.OverrideMetadata(typeof(PointThumb), new StyledPropertyMetadata<bool>(false));


        }
        public PointThumb()
        {
            this.AdornerPlacement = new PointPlacementSupport(Point);

        }
        private static void OnPointChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var pt = (PointThumb)d;
            ((PointPlacementSupport)pt.AdornerPlacement).p = (Point)e.NewValue;
            //var bndExpr = pt.GetBindingExpression(PointThumb.RelativeToPointProperty);
            //if (bndExpr != null)
            //    bndExpr.UpdateTarget();
            ((PointThumb)d).ReDraw();
        }

        public Point? RelativeToPoint
		{
			get { return (Point?)GetValue(RelativeToPointProperty); }
			set { SetValue(RelativeToPointProperty, value); }
		}

		public static readonly StyledProperty<Point?> RelativeToPointProperty =
			AvaloniaProperty.Register<PointThumb, Point?>("RelativeToPoint");
		
		protected override Type StyleKeyOverride => typeof(PointThumb);
		
		public PointThumb(Point point)
		{
			this.AdornerPlacement = new PointPlacementSupport(point);
			Point = point;
		}
 

		public AdornerPlacement AdornerPlacement { get; private set; }
		
		private class PointPlacementSupport : AdornerPlacement
		{
			public Point p;
			public PointPlacementSupport(Point point)
			{
				this.p = point;
			}

			public override void Arrange(AdornerPanel panel, Avalonia.Controls.Control adorner, Size adornedElementSize)
			{
				adorner.Arrange(new Rect(p.X, p.Y, adornedElementSize.Width, adornedElementSize.Height));
			}
		}
	}
}
