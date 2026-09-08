 
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// Allows arranging the rows/column on a grid.
	/// </summary>
	[ExtensionFor(typeof(Grid))]
	[ExtensionServer(typeof(LogicalOrExtensionServer<PrimarySelectionExtensionServer, PrimarySelectionParentExtensionServer>))]
	public class GridAdornerProvider : AdornerProvider
	{
		sealed class RowSplitterPlacement : AdornerPlacement
		{
			readonly RowDefinition row;
			public RowSplitterPlacement(RowDefinition row) { this.row = row; }
			
			public override void Arrange(AdornerPanel panel, Control adorner, Size adornedElementSize)
			{
				double rowOffset = 0;
				var grid = panel.AdornedElement as Grid;
				if (grid != null) {
					foreach (var r in grid.RowDefinitions) {
						if (r == row) break;
						rowOffset += r.ActualHeight;
					}
				}
				adorner.Arrange(new Rect(-(GridRailAdorner.RailSize + GridRailAdorner.RailDistance),
				                         rowOffset - GridRailAdorner.SplitterWidth / 2,
				                         GridRailAdorner.RailSize + GridRailAdorner.RailDistance + adornedElementSize.Width,
				                         GridRailAdorner.SplitterWidth));
			}
		}
		
		sealed class ColumnSplitterPlacement : AdornerPlacement
		{
			readonly ColumnDefinition column;
			public ColumnSplitterPlacement(ColumnDefinition column) { this.column = column; }
			
			public override void Arrange(AdornerPanel panel, Control adorner, Size adornedElementSize)
			{
				double colOffset = 0;
				var grid = panel.AdornedElement as Grid;
				if (grid != null) {
					foreach (var c in grid.ColumnDefinitions) {
						if (c == column) break;
						colOffset += c.ActualWidth;
					}
				}
				adorner.Arrange(new Rect(colOffset - GridRailAdorner.SplitterWidth / 2,
				                         -(GridRailAdorner.RailSize + GridRailAdorner.RailDistance),
				                         GridRailAdorner.SplitterWidth,
				                         GridRailAdorner.RailSize + GridRailAdorner.RailDistance + adornedElementSize.Height));
			}
		}
		
		AdornerPanel adornerPanel = new AdornerPanel();
		GridRailAdorner topBar, leftBar;
		
		protected override void OnInitialized()
		{
			leftBar = new GridRailAdorner(this.ExtendedItem, adornerPanel, Orientation.Vertical);
			topBar = new GridRailAdorner(this.ExtendedItem, adornerPanel, Orientation.Horizontal);
			
			RelativePlacement rp = new RelativePlacement(HorizontalAlignment.Left, VerticalAlignment.Stretch);
			rp.XOffset -= GridRailAdorner.RailDistance;
			AdornerPanel.SetPlacement(leftBar, rp);
			rp = new RelativePlacement(HorizontalAlignment.Stretch, VerticalAlignment.Top);
			rp.YOffset -= GridRailAdorner.RailDistance;
			AdornerPanel.SetPlacement(topBar, rp);
			
			adornerPanel.Children.Add(leftBar);
			adornerPanel.Children.Add(topBar);
			this.Adorners.Add(adornerPanel);
			
			CreateSplitter();
			this.ExtendedItem.PropertyChanged += OnPropertyChanged;
					
			base.OnInitialized();
		}

		protected override void OnRemove()
		{
			this.ExtendedItem.PropertyChanged -= OnPropertyChanged;
			base.OnRemove();
		}
		
		void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "RowDefinitions" || e.PropertyName == "ColumnDefinitions") {
				CreateSplitter();
			}
		}
		
		readonly List<GridSplitterAdorner> splitterList = new List<GridSplitterAdorner>();
		/// <summary>
		/// flag used to ensure that the asynchronus splitter creation is only enqueued once
		/// </summary>
		bool requireSplitterRecreation;
		
		void CreateSplitter()
		{
			if (requireSplitterRecreation) return;
			requireSplitterRecreation = true;
			
			Dispatcher.UIThread.Post(() => {
					requireSplitterRecreation = false;
					foreach (GridSplitterAdorner splitter in splitterList) {
						adornerPanel.Children.Remove(splitter);
					}
					splitterList.Clear();

					// Guard: ExtendedItem may have been removed from the design context
					// by the time this deferred callback runs (e.g. document reload).
					if (this.ExtendedItem?.Component is not Grid grid) return;

					IList<DesignItem> col = this.ExtendedItem.Properties["RowDefinitions"].CollectionElements;
					// Use the smaller of the two counts to avoid IndexOutOfRange when
					// the XAML model and the live Grid are momentarily out of sync.
					int rowCount = Math.Min(grid.RowDefinitions.Count, col.Count);
					for (int i = 1; i < rowCount; i++) {
						RowDefinition row = grid.RowDefinitions[i];
						GridRowSplitterAdorner splitter = new GridRowSplitterAdorner(leftBar, this.ExtendedItem, col[i-1], col[i]);
						AdornerPanel.SetPlacement(splitter, new RowSplitterPlacement(row));
						adornerPanel.Children.Add(splitter);
						splitterList.Add(splitter);
					}

					col = this.ExtendedItem.Properties["ColumnDefinitions"].CollectionElements;
					int colCount = Math.Min(grid.ColumnDefinitions.Count, col.Count);
					for (int i = 1; i < colCount; i++) {
						ColumnDefinition column = grid.ColumnDefinitions[i];
						GridColumnSplitterAdorner splitter = new GridColumnSplitterAdorner(topBar, this.ExtendedItem, col[i-1], col[i]);
						AdornerPanel.SetPlacement(splitter, new ColumnSplitterPlacement(column));
						adornerPanel.Children.Add(splitter);
						splitterList.Add(splitter);
					}
				}, DispatcherPriority.Loaded);
		}
	}
}
