
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using System.ComponentModel;
 
using Scadix.AxamlDesign.UIExtensions;
using Scadix.AxamlDesigner.Controls;

namespace Scadix.AxamlDesigner.ThumbnailView
{
	public class ThumbnailView : TemplatedControl, INotifyPropertyChanged
	{

        static ThumbnailView()
        {
            DesignSurfaceProperty.Changed.AddClassHandler<ThumbnailView>(OnDesignSurfaceChanged);
        }


        public DesignSurface DesignSurface
		{
			get { return (DesignSurface)GetValue(DesignSurfaceProperty); }
			set { SetValue(DesignSurfaceProperty, value); }
		}

		public static readonly StyledProperty<DesignSurface> DesignSurfaceProperty =
            AvaloniaProperty.Register<ThumbnailView, DesignSurface>("DesignSurface");

        private static void OnDesignSurfaceChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
		{
			var ctl = d as ThumbnailView;
			
			
			if (ctl.oldSurface != null)
				ctl.oldSurface.LayoutUpdated -= ctl.DesignSurface_LayoutUpdated;
			
			ctl.oldSurface = ctl.DesignSurface;
			ctl.scrollViewer = null;

			if (ctl.DesignSurface != null)
			{
				ctl.DesignSurface.LayoutUpdated += ctl.DesignSurface_LayoutUpdated;
			}

			ctl.OnPropertyChanged("ScrollViewer");
		}

        protected override Type StyleKeyOverride => typeof(ThumbnailView);

        public ScrollViewer ScrollViewer
		{
			get
			{
				if (DesignSurface != null && scrollViewer == null)
					scrollViewer = DesignSurface.TryFindChild<ZoomControl>();

				return scrollViewer;
			}
		}


		void DesignSurface_LayoutUpdated(object sender, EventArgs e)
		{
			if (this.scrollViewer == null)
				OnPropertyChanged("ScrollViewer");

			if (this.scrollViewer != null)
			{
				double scale, xOffset, yOffset;
				this.InvalidateScale(out scale, out xOffset, out yOffset);

				this.zoomThumb.Width = scrollViewer.Viewport.Width * scale;
				this.zoomThumb.Height = scrollViewer.Viewport.Height * scale;
				Canvas.SetLeft(this.zoomThumb, xOffset + this.ScrollViewer.Offset.X * scale);
				Canvas.SetTop(this.zoomThumb, yOffset + this.ScrollViewer.Offset.Y * scale);

				// Force VisualBrush to refresh by reassigning its Visual
				if (thumbnailBrush != null)
				{
					var v = thumbnailBrush.Visual;
					thumbnailBrush.Visual = null;
					thumbnailBrush.Visual = v;
				}
				this.zoomCanvas?.InvalidateVisual();
			}
		}

		private DesignSurface oldSurface;
		private ZoomControl scrollViewer;
		private Canvas zoomCanvas;
		private Thumb zoomThumb;
		private Avalonia.Media.VisualBrush thumbnailBrush;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            this.zoomThumb = e.NameScope.Find<Thumb>("PART_ZoomThumb");
            this.zoomCanvas = e.NameScope.Find<Canvas>("PART_ZoomCanvas");
            this.thumbnailBrush = this.zoomCanvas?.Background as Avalonia.Media.VisualBrush;

            this.zoomThumb.DragDelta += this.Thumb_DragDelta;
			this.zoomCanvas.PointerPressed += Canvas_MouseLeftButtonDown;			
		}

		private void Canvas_MouseLeftButtonDown(object sender, PointerPressedEventArgs e)
		{
			var pos = e.GetPosition(zoomCanvas);
			var cl = Canvas.GetLeft(this.zoomThumb);
			var ct = Canvas.GetTop(this.zoomThumb);

			double scale, xOffset, yOffset;
			this.InvalidateScale(out scale, out xOffset, out yOffset);
			var dl = pos.X - cl - (zoomThumb.Width / 2);
			var dt = pos.Y - ct - (zoomThumb.Height / 2);

            scrollViewer.Offset = new Avalonia.Vector(
                scrollViewer.Offset.X + dl / scale,
                scrollViewer.Offset.Y + dt / scale);
        }

		private void Thumb_DragDelta(object sender, VectorEventArgs e)
		{
			if (DesignSurface != null)
			{
				if (scrollViewer != null)
				{
					double scale, xOffset, yOffset;
					this.InvalidateScale(out scale, out xOffset, out yOffset);

                    scrollViewer.Offset = new Avalonia.Vector(
                        scrollViewer.Offset.X + e.Vector.X / scale,
                        scrollViewer.Offset.Y + e.Vector.Y / scale);
                }
			}
		}

		private void InvalidateScale(out double scale, out double xOffset, out double yOffset)
		{
			scale = 1;
			xOffset = 0;
			yOffset = 0;
			
			if (this.DesignSurface.DesignContext != null && this.DesignSurface.DesignContext.RootItem != null)
			{
				var designedElement = this.DesignSurface.DesignContext.RootItem.Component as Control;

				if (designedElement != null)
				{
					var fac1 = designedElement.DesiredSize.Width / zoomCanvas.Bounds.Width;
					var fac2 = designedElement.DesiredSize.Height / zoomCanvas.Bounds.Height;

					// zoom canvas size
					double x = this.zoomCanvas.Bounds.Width;
					double y = this.zoomCanvas.Bounds.Height;

					if (fac1 < fac2)
					{
						x = designedElement.Bounds.Width/fac2;
						xOffset = (zoomCanvas.Bounds.Width - x)/2;
						yOffset = 0;
					}
					else
					{
						y = designedElement.Bounds.Height/fac1;
						xOffset = 0;
						yOffset = (zoomCanvas.Bounds.Height - y)/2;
					}

					double w = designedElement.DesiredSize.Width;
					double h = designedElement.DesiredSize.Height;

					double scaleX = x/w;
					double scaleY = y/h;

					scale = (scaleX < scaleY) ? scaleX : scaleY;

					if (scrollViewer.Viewport.Height > h) {
						yOffset -= ((scrollViewer.Viewport.Height - h) / 2) * scale;
					}
					if (scrollViewer.Viewport.Width > w) {
						xOffset -= ((scrollViewer.Viewport.Width - w) / 2) * scale;
					}

					xOffset += (x - scale*w)/2;
					yOffset += (y - scale*h)/2;
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged(string propertyName)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
