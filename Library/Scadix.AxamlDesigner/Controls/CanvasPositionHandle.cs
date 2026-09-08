
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using System.ComponentModel;
using System.Diagnostics;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// Adorner that displays the canvas position of a control.
	/// </summary>
	public class CanvasPositionHandle : MarginHandle
	{
		protected override Type StyleKeyOverride => typeof(CanvasPositionHandle);

		static CanvasPositionHandle()
		{
			HandleLengthOffset = 2;
		}

		private Avalonia.Controls.Shapes.Path line1;
		private Avalonia.Controls.Shapes.Path line2;

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			line1 = e.NameScope.Find<Avalonia.Controls.Shapes.Path>("line1");
			line2 = e.NameScope.Find<Avalonia.Controls.Shapes.Path>("line2");
			base.OnApplyTemplate(e);
		}

		readonly Canvas canvas;
		readonly DesignItem adornedControlItem;
		readonly AdornerPanel adornerPanel;
		readonly HandleOrientation orientation;
		readonly Control adornedControl;

		public CanvasPositionHandle(DesignItem adornedControlItem, AdornerPanel adornerPanel, HandleOrientation orientation)
		{
			Debug.Assert(adornedControlItem != null);
			this.adornedControlItem = adornedControlItem;
			this.adornerPanel = adornerPanel;
			this.orientation = orientation;

			Angle = (double)orientation;

			canvas = (Canvas)adornedControlItem.Parent.Component;
			adornedControl = (Control)adornedControlItem.Component;
			Stub = new MarginStub(this);
			ShouldBeVisible = true;

			// Subscribe to property changes via INotifyPropertyChanged or AvaloniaObject
			if (adornedControl is INotifyPropertyChanged npc)
				npc.PropertyChanged += OnControlPropertyChanged;

			// Also subscribe to Avalonia property changes
			adornedControl.PropertyChanged += OnAvaloniaPropertyChanged;

			BindAndPlaceHandle();
		}

		void OnControlPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			BindAndPlaceHandle();
		}

		void OnAvaloniaPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == Canvas.LeftProperty || e.Property == Canvas.RightProperty ||
			    e.Property == Canvas.TopProperty || e.Property == Canvas.BottomProperty ||
			    e.Property == Control.WidthProperty || e.Property == Control.HeightProperty)
			{
				BindAndPlaceHandle();
			}
		}

		void OnPropertyChanged(object sender, EventArgs e)
		{
			BindAndPlaceHandle();
		}

		/// <summary>
		/// Gets/Sets the angle by which the Canvas display has to be rotated
		/// </summary>
		public override double TextTransform
		{
			get
			{
				if ((double)orientation == 90 || (double)orientation == 180)
					return 180;
				if ((double)orientation == 270)
					return 0;
				return (double)orientation;
			}
			set { }
		}

		double GetCanvasValue(AvaloniaProperty prop)
		{
			var val = adornedControl.GetValue(prop);
			return val is double d ? d : double.NaN;
		}

		void BindAndPlaceHandle()
		{
			if (!adornerPanel.Children.Contains(this))
				adornerPanel.Children.Add(this);
			if (!adornerPanel.Children.Contains(Stub))
				adornerPanel.Children.Add(Stub);

			RelativePlacement placement = new RelativePlacement();
			switch (orientation)
			{
				case HandleOrientation.Left:
				{
					var wr = GetCanvasValue(Canvas.LeftProperty);
					if (double.IsNaN(wr))
					{
						wr = GetCanvasValue(Canvas.RightProperty);
						wr = canvas.Bounds.Width - (PlacementOperation.GetRealElementSize(adornedControl).Width + wr);
					}
					else
					{
						line1?.StrokeDashArray.Clear();
						line2?.StrokeDashArray.Clear();
					}
					this.HandleLength = wr;
					placement = new RelativePlacement(HorizontalAlignment.Left, VerticalAlignment.Center);
					placement.XOffset = -HandleLengthOffset;
					break;
				}
				case HandleOrientation.Top:
				{
					var wr = GetCanvasValue(Canvas.TopProperty);
					if (double.IsNaN(wr))
					{
						wr = GetCanvasValue(Canvas.BottomProperty);
						wr = canvas.Bounds.Height - (PlacementOperation.GetRealElementSize(adornedControl).Height + wr);
					}
					else
					{
						line1?.StrokeDashArray.Clear();
						line2?.StrokeDashArray.Clear();
					}
					this.HandleLength = wr;
					placement = new RelativePlacement(HorizontalAlignment.Center, VerticalAlignment.Top);
					placement.YOffset = -HandleLengthOffset;
					break;
				}
				case HandleOrientation.Right:
				{
					var wr = GetCanvasValue(Canvas.RightProperty);
					if (double.IsNaN(wr))
					{
						wr = GetCanvasValue(Canvas.LeftProperty);
						wr = canvas.Bounds.Width - (PlacementOperation.GetRealElementSize(adornedControl).Width + wr);
					}
					else
					{
						line1?.StrokeDashArray.Clear();
						line2?.StrokeDashArray.Clear();
					}
					this.HandleLength = wr;
					placement = new RelativePlacement(HorizontalAlignment.Right, VerticalAlignment.Center);
					placement.XOffset = HandleLengthOffset;
					break;
				}
				case HandleOrientation.Bottom:
				{
					var wr = GetCanvasValue(Canvas.BottomProperty);
					if (double.IsNaN(wr))
					{
						wr = GetCanvasValue(Canvas.TopProperty);
						wr = canvas.Bounds.Height - (PlacementOperation.GetRealElementSize(adornedControl).Height + wr);
					}
					else
					{
						line1?.StrokeDashArray.Clear();
						line2?.StrokeDashArray.Clear();
					}
					this.HandleLength = wr;
					placement = new RelativePlacement(HorizontalAlignment.Center, VerticalAlignment.Bottom);
					placement.YOffset = HandleLengthOffset;
					break;
				}
			}

			AdornerPanel.SetPlacement(this, placement);
			this.IsVisible = true;
		}
	}
}
