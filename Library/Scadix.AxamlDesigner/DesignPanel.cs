
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System.ComponentModel;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesign.UIExtensions;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDesigner.Xaml;
using Avalonia.Media;

namespace Scadix.AxamlDesigner
{
	public sealed class DesignPanel : Panel, IDesignPanel, INotifyPropertyChanged
	{
		#region Hit Testing
		
		private List<DesignItem> hitTestElements = new List<DesignItem>();
		private List<DesignItem> skippedHitTestElements = new List<DesignItem>();


		/// <summary>
		/// this element is always hit (unless HitTestVisible is set to false)
		/// </summary>
		sealed class EatAllHitTestRequests : Border
        {
			// In Avalonia, hit testing is handled differently - this control simply accepts all hits
			protected override void OnPointerPressed(PointerPressedEventArgs e)
			{
				// Accept all pointer events
			}
		}
		
		// Track last known key modifiers from key events
		private KeyModifiers _lastKeyModifiers = KeyModifiers.None;

		void RunHitTest(Visual reference, Point point, Func<Visual, bool> filterCallback, Func<Visual, bool> resultCallback)
		{
			if ((_lastKeyModifiers & KeyModifiers.Alt) == 0)
			{
				hitTestElements.Clear();
				skippedHitTestElements.Clear();
			}

			// Avalonia hit testing via VisualExtensions
			var hits = reference.GetVisualsAt(point);
			foreach (var visual in hits)
			{
				if (filterCallback != null && !filterCallback(visual))
					continue;
				if (resultCallback != null && !resultCallback(visual))
					break;
			}
		}

		bool FilterHitTestInvisibleElements(Visual potentialHitTestTarget, HitTestType hitTestType)
		{
			Control element = potentialHitTestTarget as Control;
			
			if (element != null) {
				if (!(element.IsHitTestVisible && element.IsVisible)) {
					return false; // skip self and children
				}
				
				var designItem = Context.Services.Component.GetDesignItem(element) as XamlDesignItem;

				if (hitTestType == HitTestType.ElementSelection)
				{
					if ((_lastKeyModifiers & KeyModifiers.Alt) != 0)
					{
						if (designItem != null)
						{
							if (skippedHitTestElements.LastOrDefault() == designItem ||
							    (hitTestElements.Contains(designItem) && !skippedHitTestElements.Contains(designItem)))
							{
								skippedHitTestElements.Remove(designItem);
								return false; // skip self and children
							}
						}
					}
				}
				else
				{
					hitTestElements.Clear();
					skippedHitTestElements.Clear();
				}

				if (designItem != null && designItem.IsDesignTimeLocked) {
					return false; // skip self and children
				}

				if (designItem != null && !hitTestElements.Contains(designItem))
				{
					hitTestElements.Add(designItem);
					skippedHitTestElements.Add(designItem);
				}
			}
			
			return true; // continue
		}
		
		/// <summary>
		/// Performs a custom hit testing lookup for the specified mouse event args.
		/// </summary>
		public DesignPanelHitTestResult HitTest(Point mousePosition, bool testAdorners, bool testDesignSurface, HitTestType hitTestType)
		{
			DesignPanelHitTestResult result = DesignPanelHitTestResult.NoHit;
			HitTest(mousePosition, testAdorners, testDesignSurface,
			        delegate(DesignPanelHitTestResult r) {
			        	result = r;
			        	return false;
			        }, hitTestType);
			
			return result;
		}

		/// <summary>
		/// Performs a hit test on the design surface, raising <paramref name="callback"/> for each match.
		/// Hit testing continues while the callback returns true.
		/// </summary>
		public void HitTest(Point mousePosition, bool testAdorners, bool testDesignSurface, Predicate<DesignPanelHitTestResult> callback, HitTestType hitTestType)
		{
			if (mousePosition.X < 0 || mousePosition.Y < 0 || mousePosition.X > this.Bounds.Width || mousePosition.Y > this.Bounds.Height) {
				return;
			}

			bool continueHitTest = true;

			Func<Visual, bool> filterBehavior = CustomHitTestFilterBehavior ?? (x => FilterHitTestInvisibleElements(x, hitTestType));
			CustomHitTestFilterBehavior = null;

			if (testAdorners) {
				RunHitTest(
					_adornerLayer, mousePosition, filterBehavior,
					delegate(Visual visualHit) {
						if (visualHit != null) {
							DesignPanelHitTestResult customResult = new DesignPanelHitTestResult(visualHit);
							Visual obj = visualHit;
							while (obj != null && obj != _adornerLayer) {
								AdornerPanel adorner = obj as AdornerPanel;
								if (adorner != null) {
									customResult.AdornerHit = adorner;
								}
								obj = obj.GetVisualParent();
							}
							continueHitTest = callback(customResult);
							return continueHitTest;
						}
						return true;
					});
			}

			if (continueHitTest && testDesignSurface) {
				RunHitTest(
					_child, mousePosition, filterBehavior,
					delegate(Visual visualHit) {
						if (visualHit != null) {
							DesignPanelHitTestResult customResult = new DesignPanelHitTestResult(visualHit);

							ViewService viewService = _context.Services.View;
							Visual obj = visualHit;
							
							while (obj != null) {
								if ((customResult.ModelHit = viewService.GetModel(obj)) != null)
									break;
								obj = obj.GetVisualParent();
							}
							if (customResult.ModelHit == null) {
								customResult.ModelHit = _context.RootItem;
							}
							
							continueHitTest = callback(customResult);
							return continueHitTest;
						}
						return true;
					}
				);
			}
		}
		#endregion
		
		#region Fields + Constructor
		DesignContext _context;
		readonly EatAllHitTestRequests _eatAllHitTestRequests;
		readonly AdornerLayer _adornerLayer;
		
		public DesignPanel()
		{
			this.Focusable = true;
		 
			
			_eatAllHitTestRequests = new EatAllHitTestRequests();
            _eatAllHitTestRequests.Background = Brushes.Transparent;
            _eatAllHitTestRequests.PointerPressed += delegate {
				this.Focus();
			};
            DragDrop.SetAllowDrop(_eatAllHitTestRequests, true);
          
			_adornerLayer = new AdornerLayer(this);
			
			this.KeyUp += DesignPanel_KeyUp;
			this.KeyDown += DesignPanel_KeyDown;

            // Wire up IDesignPanel events

            AddHandler(DragDrop.DragEnterEvent, HandleDragEnter);
            AddHandler(DragDrop.DragOverEvent, HandleDragOver);
            AddHandler(DragDrop.DragLeaveEvent, HandleDragLeave);
            AddHandler(DragDrop.DropEvent, HandleDrop);
        }
		#endregion
		
		#region IDesignPanel Events
	 
		public event EventHandler<DragEventArgs> DragEnter;
		public event EventHandler<DragEventArgs> DragOver;
		public event EventHandler<DragEventArgs> DragLeave;
		public event EventHandler<DragEventArgs> Drop;
		#endregion
		
		#region Properties
		
		public DesignSurface DesignSurface { get; internal set; }
		
		//Set custom HitTestFilterCallback
		public Func<Visual, bool> CustomHitTestFilterBehavior { get; set; }

		public AdornerLayer AdornerLayer
		{
			get
			{
				return _adornerLayer;
			}
		}

		/// <summary>
		/// Gets/Sets the design context.
		/// </summary>
		public DesignContext Context {
			get { return _context; }
			set { _context = value; }
		}
		
		public ICollection<AdornerPanel> Adorners {
			get {
				return _adornerLayer.Adorners;
			}
		}
		
		/// <summary>
		/// Gets/Sets if the design content is visible for hit-testing purposes.
		/// </summary>
		public bool IsContentHitTestVisible {
			get { return !_eatAllHitTestRequests.IsHitTestVisible; }
			set { _eatAllHitTestRequests.IsHitTestVisible = !value; }
		}
		
		/// <summary>
		/// Gets/Sets if the adorner layer is visible for hit-testing purposes.
		/// </summary>
		public bool IsAdornerLayerHitTestVisible {
			get { return _adornerLayer.IsHitTestVisible; }
			set { _adornerLayer.IsHitTestVisible = value; }
		}
		
		/// <summary>
		/// Enables / Disables the Snapline Placement
		/// </summary>
		private bool _useSnaplinePlacement = true;
		public bool UseSnaplinePlacement {
			get { return _useSnaplinePlacement; }
			set {
				if (_useSnaplinePlacement != value) {
					_useSnaplinePlacement = value;
					OnPropertyChanged("UseSnaplinePlacement");
				}
			}
		}
		
		/// <summary>
		/// Enables / Disables the Raster Placement
		/// </summary>
		private bool _useRasterPlacement = false;
		public bool UseRasterPlacement {
			get { return _useRasterPlacement; }
			set {
				if (_useRasterPlacement != value) {
					_useRasterPlacement = value;
					OnPropertyChanged("UseRasterPlacement");
				}
			}
		}
		
		/// <summary>
		/// Sets the with of the Raster when using Raster Placement
		/// </summary>
		private int _rasterWidth = 5;
		public int RasterWidth {
			get { return _rasterWidth; }
			set {
				if (_rasterWidth != value) {
					_rasterWidth = value;
					OnPropertyChanged("RasterWidth");
				}
			}
		}
		
		#endregion
		
		#region Visual Child Management
		private Control _child;

		public Control Child {
			get { return _child; }
			set {
				if (_child == value)
					return;
				if (_child != null) {
					Children.Remove(_adornerLayer);
					Children.Remove(_eatAllHitTestRequests);
					Children.Remove(_child);
				}
				_child = value;
				if (_child != null) {
					Children.Add(_child);
					Children.Add(_eatAllHitTestRequests);
					Children.Add(_adornerLayer);
				}
			}
		}
		
		protected override Size MeasureOverride(Size constraint)
		{
			Size result = base.MeasureOverride(constraint);
			if (_child != null) {
				_adornerLayer.Measure(constraint);
				_eatAllHitTestRequests.Measure(constraint);
			}
			return result;
		}
		
		protected override Size ArrangeOverride(Size arrangeSize)
		{
			Size result = base.ArrangeOverride(arrangeSize);
			if (_child != null) {
				Rect r = new Rect(new Point(0, 0), arrangeSize);
				_adornerLayer.Arrange(r);
				_eatAllHitTestRequests.Arrange(r);
			}
			return result;
		}
		#endregion
		
		PlacementOperation placementOp;
		Dictionary<PlacementInformation, int> dx = new Dictionary<PlacementInformation, int>();
		Dictionary<PlacementInformation, int> dy = new Dictionary<PlacementInformation, int>();
		
		/// <summary>
		/// If interface implementing class sets this to false defaultkeyaction will be 
		/// </summary>
		/// <param name="e"></param>
		/// <returns></returns>
		bool InvokeDefaultKeyDownAction(Extension e)
		{
			var keyDown = e as IKeyDown;
			if (keyDown != null) {
				return keyDown.InvokeDefaultAction;
			}
			
			return true;
		}
		
		private void DesignPanel_KeyUp(object sender, KeyEventArgs e)
		{
			_lastKeyModifiers = e.KeyModifiers;
			if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
			{
				e.Handled = true;
				
				if (placementOp != null) {
					placementOp.Commit();
					placementOp = null;
					dx.Clear();
					dy.Clear();
				}
			}
			//pass the key event to the underlying objects if they have implemented IKeyUp interface
			//OBS!!!! this call needs to be here, after the placementOp.Commit().
			//In case the underlying object has a operation of its own this operation needs to be commited first
			foreach (DesignItem di in Context.Services.Selection.SelectedItems.Reverse()) {
				foreach (Extension ext in di.Extensions) {
					var keyUp = ext as IKeyUp;
					if (keyUp != null) {
						keyUp.KeyUpAction(sender, e);
		}
				}
			}
		}
		
		void DesignPanel_KeyDown(object sender, KeyEventArgs e)
		{
			_lastKeyModifiers = e.KeyModifiers;
			//pass the key event down to the underlying objects if they have implemented IKeyUp interface
			//OBS!!!! this call needs to be here, before the PlacementOperation.Start.
			//In case the underlying object has a operation of its own this operation needs to be set first
			foreach (DesignItem di in Context.Services.Selection.SelectedItems) {
				foreach (Extension ext in di.Extensions) {
					var keyDown = ext as IKeyDown;
					if (keyDown != null) {
						keyDown.KeyDownAction(sender, e);
					}
				}
			}

			if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down) {
				bool initialEvent = false;

				e.Handled = true;

				PlacementType placementType = (e.KeyModifiers & KeyModifiers.Control) != 0 ? PlacementType.Resize : PlacementType.MoveAndIgnoreOtherContainers;
				
				if (placementOp != null && placementOp.Type != placementType) {
					placementOp.Commit();
					placementOp = null;
					dx.Clear();
					dy.Clear();
				}
				
				if (placementOp == null) {
					
					//check if any objects don't want the default action to be invoked
					List<DesignItem> placedItems = Context.Services.Selection.SelectedItems.Where(x => x.Extensions.All(InvokeDefaultKeyDownAction)).ToList();
					
					//if no remaining objects, break
					if (placedItems.Count < 1) return;
										
					placementOp = PlacementOperation.Start(placedItems, placementType);

					dx.Clear();
					dy.Clear();
				}

				int odx = 0, ody = 0;
				switch (e.Key) {
					case Key.Left:
						odx = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? -10 : -1;
						break;
					case Key.Up:
						ody = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? -10 : -1;
						break;
					case Key.Right:
						odx = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? 10 : 1;
						break;
					case Key.Down:
						ody = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? 10 : 1;
						break;
				}

				foreach (PlacementInformation info in placementOp.PlacedItems) {
					if (!dx.ContainsKey(info)) {
						dx[info] = 0;
						dy[info] = 0;
					}
					var transform = info.Item.Parent.View.TransformToVisual(this);
					if (transform.HasValue) {
						var matrix = transform.Value;
						var angle = Math.Atan2(matrix.M21, matrix.M11) * 180 / Math.PI;
						if (angle > 45.0 && angle < 135.0) {
							dx[info] += ody * -1;
							dy[info] += odx;
						} else if (angle < -45.0 && angle > -135.0) {
							dx[info] += ody;
							dy[info] += odx * -1;
						} else if (angle > 135.0 || angle < -135.0) {
							dx[info] += odx * -1;
							dy[info] += ody * -1;
						} else {
							dx[info] += odx;
							dy[info] += ody;
						}
					}

					var bounds = info.OriginalBounds;
					
					if (placementType == PlacementType.Move 
						|| info.Operation.Type == PlacementType.MoveAndIgnoreOtherContainers) {
						info.Bounds = new Rect(bounds.Left + dx[info],
						                       bounds.Top + dy[info],
						                       bounds.Width,
						                       bounds.Height);
					} else if (placementType == PlacementType.Resize) {
						if (bounds.Width + dx[info] >= 0 && bounds.Height + dy[info] >= 0)  {
							info.Bounds = new Rect(bounds.Left,
							                       bounds.Top,
							                       bounds.Width + dx[info],
							                       bounds.Height + dy[info]);
						}
					}
					
					placementOp.CurrentContainerBehavior.SetPosition(info);
				}
			}
		}
		
		static bool IsPropertySet(Control element, AvaloniaProperty d)
		{
			return element.IsSet(d);
		}
		
		protected override void OnPointerMoved(PointerEventArgs e)
		{
			base.OnPointerMoved(e);
            if (Context != null)
            {
                var cursor = Context.Services.Tool.CurrentTool.Cursor;
                this.Cursor = cursor ?? Cursor.Default;
            }
        }

        private void HandleDragEnter(object sender, DragEventArgs e)
        {
            DragEnter?.Invoke(this, e);
            e.DragEffects = DragDropEffects.Copy;
        }

        private void HandleDragOver(object sender, DragEventArgs e)
        {
            DragOver?.Invoke(this, e);
            e.DragEffects = DragDropEffects.Copy;
        }

        private void HandleDragLeave(object sender, DragEventArgs e)
        {
            DragLeave?.Invoke(this, e);
        }

        private void HandleDrop(object sender, DragEventArgs e)
        {
            Drop?.Invoke(this, e);
            e.DragEffects = DragDropEffects.Copy;
        }

        public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string propertyName)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
		}
		
		#region ContextMenu

		private Dictionary<ContextMenu, Tuple<int,List<object>>> contextMenusAndEntries = new Dictionary<ContextMenu, Tuple<int,List<object>>>();

		public Action<ContextMenu> ContextMenuHandler { get; set; }

		public void AddContextMenu(ContextMenu contextMenu)
		{
			AddContextMenu(contextMenu, int.MaxValue);
		}

		public void AddContextMenu(ContextMenu contextMenu, int orderIndex)
		{
			contextMenusAndEntries.Add(contextMenu, new Tuple<int, List<object>>(orderIndex, new List<object>(contextMenu.Items.Cast<object>())));
			contextMenu.Items.Clear();

			UpdateContextMenu();
		}

		public void RemoveContextMenu(ContextMenu contextMenu)
		{
			contextMenusAndEntries.Remove(contextMenu);
			
			UpdateContextMenu();
		}

		public void ClearContextMenu()
		{
			contextMenusAndEntries.Clear();
			ContextMenu = null;
		}

		private void UpdateContextMenu()
		{
			if (this.ContextMenu != null)
			{
				this.ContextMenu.Items.Clear();
				this.ContextMenu = null;
			}
			
			var contextMenu = new ContextMenu();
			
			foreach (var entries in contextMenusAndEntries.Values.OrderBy(x => x.Item1).Select(x => x.Item2))
			{
				if (contextMenu.Items.Count > 0)
					contextMenu.Items.Add(new Separator());

				foreach (var entry in entries)
				{
					var ctl = ((Control)entry).TryFindParent<ItemsControl>();
					if (ctl != null)
						ctl.Items.Remove(entry);
					contextMenu.Items.Add(entry);
				}
			}
			
			if (ContextMenuHandler != null)
				ContextMenuHandler(contextMenu);
			else
				this.ContextMenu = contextMenu;
		}

		#endregion
	}
}
