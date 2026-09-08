
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;

namespace Scadix.AxamlDesigner.Controls
{
	/// <summary>
	/// A control that displays adorner panels.
	/// </summary>
	public sealed class AdornerLayer : Panel, IAdornerLayer
	{
		#region AdornerPanelCollection
		internal sealed class AdornerPanelCollection : ICollection<AdornerPanel>, IReadOnlyCollection<AdornerPanel>
		{
			readonly AdornerLayer _layer;
			
			public AdornerPanelCollection(AdornerLayer layer)
			{
				this._layer = layer;
			}
			
			public int Count {
				get { return _layer.Children.Count; }
			}
			
			public bool IsReadOnly {
				get { return false; }
			}
			
			public void Add(AdornerPanel item)
			{
				if (item == null)
					throw new ArgumentNullException("item");
				
				_layer.AddAdorner(item);
			}
			
			public void Clear()
			{
				_layer.ClearAdorners();
			}
			
			public bool Contains(AdornerPanel item)
			{
				if (item == null)
					throw new ArgumentNullException("item");
				
				return item.GetVisualParent() == _layer;
			}
			
			public void CopyTo(AdornerPanel[] array, int arrayIndex)
			{
				foreach (AdornerPanel panel in this)
					array[arrayIndex++] = panel;
			}
			
			public bool Remove(AdornerPanel item)
			{
				if (item == null)
					throw new ArgumentNullException("item");
				
				return _layer.RemoveAdorner(item);
			}
			
			public IEnumerator<AdornerPanel> GetEnumerator()
			{
				foreach (AdornerPanel panel in _layer.Children) {
					yield return panel;
				}
			}
			
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}
		#endregion
		
		AdornerPanelCollection _adorners;
		readonly Control _designPanel;
		
		#if DEBUG_ADORNERLAYER
		int _totalAdornerCount;
		#endif
		
		internal AdornerLayer(Control designPanel)
		{
			this._designPanel = designPanel;
			
			this.LayoutUpdated += OnLayoutUpdated;
			
			_adorners = new AdornerPanelCollection(this);
		}
		
		void OnLayoutUpdated(object? sender, EventArgs e)
		{
			UpdateAllAdorners(false);
		}
		
		protected override void OnSizeChanged(SizeChangedEventArgs e)
		{
			base.OnSizeChanged(e);
			UpdateAllAdorners(true);
		}
		
		internal AdornerPanelCollection Adorners {
			get {
				return _adorners;
			}
		}
		
		sealed class AdornerInfo
		{
			internal readonly List<AdornerPanel> adorners = new List<AdornerPanel>();
			internal bool isVisible;
			internal Rect position;
		}
		
		// adorned element => AdornerInfo
		Dictionary<Control, AdornerInfo> _dict = new Dictionary<Control, AdornerInfo>();
		
		void ClearAdorners()
		{
			if (_dict.Count == 0)
				return; // already empty
			
			this.Children.Clear();
			_dict = new Dictionary<Control, AdornerInfo>();
		}
		
		AdornerInfo GetOrCreateAdornerInfo(Control adornedElement)
		{
			AdornerInfo info;
			if (!_dict.TryGetValue(adornedElement, out info)) {
				info = _dict[adornedElement] = new AdornerInfo();
				info.isVisible = adornedElement.IsVisualAncestorOf(_designPanel) || _designPanel.IsVisualAncestorOf(adornedElement);
			}
			return info;
		}
		
		AdornerInfo? GetExistingAdornerInfo(Control adornedElement)
		{
			AdornerInfo? info;
			_dict.TryGetValue(adornedElement, out info);
			return info;
		}
		
		void AddAdorner(AdornerPanel adornerPanel)
		{
			if (adornerPanel.AdornedElement == null)
				throw new DesignerException("adornerPanel.AdornedElement must be set");
			
			AdornerInfo info = GetOrCreateAdornerInfo(adornerPanel.AdornedElement);
			info.adorners.Add(adornerPanel);
			
			if (info.isVisible) {
				AddAdornerToChildren(adornerPanel);
			}
		}
		
		void AddAdornerToChildren(AdornerPanel adornerPanel)
		{
			var children = this.Children;
			int i = 0;
			for (i = 0; i < children.Count; i++) {
				AdornerPanel p = (AdornerPanel)children[i];
				if (p.Order > adornerPanel.Order) {
					break;
				}
			}
			children.Insert(i, adornerPanel);
		}
		
		protected override Size MeasureOverride(Size availableSize)
		{
			Size infiniteSize = new Size(double.PositiveInfinity, double.PositiveInfinity);
			foreach (AdornerPanel adorner in this.Children) {
				adorner.Measure(infiniteSize);
			}
			return new Size(0, 0);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			foreach (AdornerPanel adorner in this.Children) {
				if (_designPanel.IsVisualAncestorOf(adorner.AdornedElement))
				{
					var matrix = adorner.AdornedElement.TransformToVisual(_designPanel);
                   
                    if (matrix.HasValue)
					{
						var m = matrix.Value;
						if (adorner.AdornedDesignItem != null && adorner.AdornedDesignItem.Parent != null && adorner.AdornedDesignItem.Parent.View is Canvas && adorner.AdornedElement.Bounds.Height == 0 && adorner.AdornedElement.Bounds.Width == 0)
						{
							var width = ((Control)adorner.AdornedElement).Width;
							width = width > 0 ? width : 2.0;
							var height = ((Control)adorner.AdornedElement).Height;
							height = height > 0 ? height : 2.0;
							var xOffset = m.M31 - (width / 2);
							var yOffset = m.M32 - (height / 2);
							adorner.RenderTransform = new MatrixTransform(new Matrix(m.M11, m.M12, m.M21, m.M22, xOffset, yOffset));
						}
						else
						{
							adorner.RenderTransform = new MatrixTransform(m);
						}
					}


				 
				}

				adorner.Arrange(new Rect(new Point(0, 0), adorner.DesiredSize));
			}
			return finalSize;
		}
		
		bool RemoveAdorner(AdornerPanel adornerPanel)
		{
			if (adornerPanel.AdornedElement == null)
				return false;
			
			AdornerInfo? info = GetExistingAdornerInfo(adornerPanel.AdornedElement);
			if (info == null)
				return false;
			
			if (info.adorners.Remove(adornerPanel)) {
				if (info.isVisible) {
					this.Children.Remove(adornerPanel);
				}
				
				if (info.adorners.Count == 0) {
					_dict.Remove(adornerPanel.AdornedElement);
				}
				
				return true;
			} else {
				return false;
			}
		}
		
		public void UpdateAdornersForElement(Control element, bool forceInvalidate)
		{
			AdornerInfo? info = GetExistingAdornerInfo(element);
			if (info != null) {
				UpdateAdornersForElement(element, info, forceInvalidate);
			}
		}
		
		Rect GetPositionCache(Control element)
		{
			var matrix = element.TransformToVisual(_designPanel);
			var p = matrix.HasValue ? matrix.Value.Transform(new Point(0, 0)) : new Point(0, 0);
			return new Rect(p, element.Bounds.Size);
		}
		
		void UpdateAdornersForElement(Control element, AdornerInfo info, bool forceInvalidate)
		{
			if (_designPanel.IsVisualAncestorOf(element)) {
				if (!info.isVisible) {
					info.isVisible = true;
					// make adorners visible:
					info.adorners.ForEach(AddAdornerToChildren);
				}
				Rect c = GetPositionCache(element);
				if (forceInvalidate || !info.position.Equals(c)) {
					info.position = c;
					foreach (AdornerPanel p in info.adorners) {
						p.InvalidateMeasure();
					}
					this.InvalidateArrange();
				}
			} else {
				if (info.isVisible) {
					info.isVisible = false;
					// make adorners invisible:
					foreach (var p in info.adorners)
						this.Children.Remove(p);
				}
			}
		}
		
		void UpdateAllAdorners(bool forceInvalidate)
		{
			foreach (KeyValuePair<Control, AdornerInfo> pair in _dict) {
				UpdateAdornersForElement(pair.Key, pair.Value, forceInvalidate);
			}
		}
	}
}
