 

using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDesigner.Services;
using Avalonia;

namespace Scadix.AxamlDesigner.Extensions
{
	
	/// <summary>
	/// The drag handle displayed for Framework Elements
	/// </summary>
	[ExtensionServer(typeof(PrimarySelectionButOnlyWhenMultipleSelectedExtensionServer))]
	[ExtensionFor(typeof(Control))]
	public class TopLeftContainerDragHandleMultipleItems : AdornerProvider
	{
		/// <summary/>
		public TopLeftContainerDragHandleMultipleItems()
		{ }
		
		protected override void OnInitialized()
		{
			base.OnInitialized();
			
			ContainerDragHandle rect = new ContainerDragHandle();
			
			rect.PointerPressed += delegate(object sender, PointerPressedEventArgs e) {
				//Services.Selection.SetSelectedComponents(new DesignItem[] { this.ExtendedItem }, SelectionTypes.Auto);
				new DragMoveMouseGesture(this.ExtendedItem, false).Start(this.ExtendedItem.Services.DesignPanel,e);
				e.Handled=true;
			};
			
			var items = this.ExtendedItem.Services.Selection.SelectedItems;
			
			double minX = 0;
			double minY = 0;
			double maxX = 0;
			double maxY = 0;
			
			foreach (DesignItem di in items) {
				Point relativeLocation = di.View.TranslatePoint(new Point(0, 0), this.ExtendedItem.View) ?? new Point();
				
				minX = minX < relativeLocation.X ? minX : relativeLocation.X;
				minY = minY < relativeLocation.Y ? minY : relativeLocation.Y;
				maxX = maxX > relativeLocation.X + ((Control)di.View).Bounds.Width ? maxX : relativeLocation.X + ((Control)di.View).Bounds.Width;
				maxY = maxY > relativeLocation.Y + ((Control)di.View).Bounds.Height ? maxY : relativeLocation.Y + ((Control)di.View).Bounds.Height;
			}
			
			Rectangle rect2 = new Rectangle() {
				Width = (maxX - minX) + 4,
				Height = (maxY - minY) + 4,
				Stroke = Brushes.Black,
				StrokeThickness = 2,
				StrokeDashArray = new AvaloniaList<double>(){ 2, 2 },
			};
			
			RelativePlacement p = new RelativePlacement(HorizontalAlignment.Left, VerticalAlignment.Top);
			p.XOffset = minX - 3;
			p.YOffset = minY - 3;
			
			RelativePlacement p2 = new RelativePlacement(HorizontalAlignment.Left, VerticalAlignment.Top);
			p2.XOffset = (minX + rect2.Width) - 2;
			p2.YOffset = (minY + rect2.Height) - 2;
						
			AddAdorner(p, AdornerOrder.Background, rect);
			AddAdorner(p2, AdornerOrder.Background, rect2);
		}
	}	
}

