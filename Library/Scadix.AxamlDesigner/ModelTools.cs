

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using System.IO;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Xaml;

namespace Scadix.AxamlDesigner
{
	/// <summary>
	/// Static helper methods for working with the designer DOM.
	/// </summary>
	public static class ModelTools
	{
		/// <summary>
		/// Compares the positions of a and b in the model file.
		/// </summary>
		public static int ComparePositionInModelFile(DesignItem a, DesignItem b)
		{
			// first remember all parent properties of a
			HashSet<DesignItemProperty> aProps = new HashSet<DesignItemProperty>();
			DesignItem tmp = a;
			while (tmp != null) {
				aProps.Add(tmp.ParentProperty);
				tmp = tmp.Parent;
			}
			
			// now walk up b's parent tree until a matching property is found
			tmp = b;
			while (tmp != null) {
				DesignItemProperty prop = tmp.ParentProperty;
				if (aProps.Contains(prop)) {
					if (prop.IsCollection) {
						return prop.CollectionElements.IndexOf(a).CompareTo(prop.CollectionElements.IndexOf(b));
					} else {
						return 0;
					}
				}
				tmp = tmp.Parent;
			}
			return 0;
		}
		
		/// <summary>
		/// Gets if the specified design item is in the document it belongs to.
		/// </summary>
		/// <returns>True for live objects, false for deleted objects.</returns>
		public static bool IsInDocument(DesignItem item)
		{
			DesignItem rootItem = item.Context.RootItem;
			while (item != null) {
				if (item == rootItem) return true;
				item = item.Parent;
			}
			return false;
		}
		
		/// <summary>
		/// Gets if the specified components can be deleted.
		/// </summary>
		public static bool CanDeleteComponents(ICollection<DesignItem> items)
		{
			IPlacementBehavior b = PlacementOperation.GetPlacementBehavior(items);
			return b != null
				&& b.CanPlace(items, PlacementType.Delete, PlacementAlignment.Center);
		}

		public static bool CanSelectComponent(DesignItem item)
		{
			return item.View != null;
		}
		
		/// <summary>
		/// Deletes the specified components from their parent containers.
		/// If the deleted components are currently selected, they are deselected before they are deleted.
		/// </summary>
		public static void DeleteComponents(ICollection<DesignItem> deleteItems)
		{
			if (deleteItems.Count > 0) {
				var changeGroup = deleteItems.First().OpenGroup("Delete Items");
				try {
					var itemsGrpParent = deleteItems.GroupBy(x => x.Parent);
					foreach (var itemsList in itemsGrpParent) {
						var items = itemsList.ToList();
						DesignItem parent = items.First().Parent;
						PlacementOperation operation = PlacementOperation.Start(items, PlacementType.Delete);
						try {
							ISelectionService selectionService = items.First().Services.Selection;
							selectionService.SetSelectedComponents(items, SelectionTypes.Remove);
							// if the selection is empty after deleting some components, select the parent of the deleted component
							if (selectionService.SelectionCount == 0 && !items.Contains(parent)) {
								selectionService.SetSelectedComponents(new[] {parent});
							}
							foreach (var designItem in items) {
								designItem.Name = null;
							}

							var service = parent.Services.Component as XamlComponentService;
							foreach (var item in items) {
								service.RaiseComponentRemoved(item);
							}

							operation.DeleteItemsAndCommit();
						} catch {
							operation.Abort();
							throw;
						}
					}
					changeGroup.Commit();
				} catch {
					changeGroup.Abort();
					throw;
				}
			}
		}
		
		public static void CreateVisualTree(this Control element)
		{
			// WPF XPS/FixedDocument not available in Avalonia - force measure/arrange instead
			try
			{
				if (element is Avalonia.Controls.Primitives.TemplatedControl templatedControl)
				{
					templatedControl.ApplyTemplate();
				}
				element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
				element.Arrange(new Rect(element.DesiredSize));
			}
			catch (Exception)
			{ }
		}
		
		internal static Size GetDefaultSize(DesignItem createdItem)
		{
			var defS = Metadata.GetDefaultSize(createdItem.ComponentType, false);
			if (defS != null)
				return defS.Value;

			// CreateVisualTree(createdItem.View);
			// var s = createdItem.View.DesiredSize;
			var s = new Size(0, 0);
			
			var newS = Metadata.GetDefaultSize(createdItem.ComponentType, true);

			if (newS.HasValue)
			{
				double sw = s.Width, sh = s.Height;
				if (!(sw > 5) && newS.Value.Width > 0)
					sw = newS.Value.Width;
				if (!(sh > 5) && newS.Value.Height > 0)
					sh = newS.Value.Height;
				s = new Size(sw, sh);
			}

			double sWidth = s.Width, sHeight = s.Height;
			if (double.IsNaN(sWidth) && GetWidth(createdItem.View) > 0) {
				sWidth = GetWidth(createdItem.View);
			}
			if (double.IsNaN(sHeight) && GetHeight(createdItem.View) > 0) {
				sHeight = GetHeight(createdItem.View);
			}

			if (sWidth == 0 && sHeight == 0) {
				sWidth = 100;
				sHeight = 100;
			}

			return new Size(sWidth, sHeight);
		}
		
		public static double GetWidth(Control element)
		{
			double v = (double)element.GetValue(Control.WidthProperty);
			if (double.IsNaN(v))
				return element.Bounds.Width;
			else
				return v;
		}

		public static double GetHeight(Control element)
		{
			double v = (double)element.GetValue(Control.HeightProperty);
			if (double.IsNaN(v))
				return element.Bounds.Height;
			else
				return v;
		}

		public static void Resize(DesignItem item, double newWidth, double newHeight)
		{
			if (newWidth != GetWidth(item.View)) {
				if(double.IsNaN(newWidth))
					item.Properties.GetProperty(Control.WidthProperty).Reset();
				else
					item.Properties.GetProperty(Control.WidthProperty).SetValue(newWidth);
			}
			if (newHeight != GetHeight(item.View)) {
				if (double.IsNaN(newHeight))
					item.Properties.GetProperty(Control.HeightProperty).Reset();
				else
					item.Properties.GetProperty(Control.HeightProperty).SetValue(newHeight);
			}
		}
		
		
		private class ItemPos
		{
			public HorizontalAlignment HorizontalAlignment{ get; set; }
			
			public VerticalAlignment VerticalAlignment{ get; set; }
			
			public double Xmin { get; set; }
			
			public double Xmax { get; set; }

			public double Ymin { get; set; }
			
			public double Ymax { get; set; }
			
			public DesignItem DesignItem { get; set; }
		}

		private static ItemPos GetItemPos(PlacementOperation operation, DesignItem designItem)
		{
			var itemPos = new ItemPos() {DesignItem = designItem};

			var pos = operation.CurrentContainerBehavior.GetPosition(operation, designItem);
			itemPos.Xmin = pos.X;
			itemPos.Xmax = pos.X + pos.Width;
			itemPos.Ymin = pos.Y;
			itemPos.Ymax = pos.Y + pos.Height;

			return itemPos;
		}

		public static Tuple<DesignItem, Rect> WrapItemsNewContainer(IEnumerable<DesignItem> items, Type containerType, bool doInsert = true)
		{
			var collection = items;
			
			var _context = collection.First().Context as XamlDesignContext;
			
			var container = collection.First().Parent;
			
			if (collection.Any(x => x.Parent != container))
				return null;

			//Change Code to use the Placment Operation!
			var placement = container.Extensions.OfType<IPlacementBehavior>().FirstOrDefault();
			if (placement == null)
				return null;

			var operation = PlacementOperation.Start(items.ToList(), PlacementType.Move);

			var newInstance = _context.Services.ExtensionManager.CreateInstanceWithCustomInstanceFactory(containerType, null);
			DesignItem newPanel = _context.Services.Component.RegisterComponentForDesigner(newInstance);
			
			List<ItemPos> itemList = new List<ItemPos>();

			int? firstIndex = null;

			foreach (var item in collection) {
				itemList.Add(GetItemPos(operation, item));
				//var pos = placement.GetPosition(null, item);
				if (container.Component is Canvas) {
					item.Properties.GetAttachedProperty(Canvas.RightProperty).Reset();
					item.Properties.GetAttachedProperty(Canvas.LeftProperty).Reset();
					item.Properties.GetAttachedProperty(Canvas.TopProperty).Reset();
					item.Properties.GetAttachedProperty(Canvas.BottomProperty).Reset();
				} else if (container.Component is Grid) {
					item.Properties.GetProperty(Control.HorizontalAlignmentProperty).Reset();
					item.Properties.GetProperty(Control.VerticalAlignmentProperty).Reset();
					item.Properties.GetProperty(Control.MarginProperty).Reset();
				}

				if (item.ParentProperty.IsCollection) {
					var parCol = item.ParentProperty.CollectionElements;
					if (!firstIndex.HasValue)
						firstIndex = parCol.IndexOf(item);
					parCol.Remove(item);
				}
				else {
					item.ParentProperty.Reset();
				}
			}
			
			var xmin = itemList.Min(x => x.Xmin);
			var xmax = itemList.Max(x => x.Xmax);
			var ymin = itemList.Min(x => x.Ymin);
			var ymax = itemList.Max(x => x.Ymax);

			foreach (var item in itemList) {
				if (newPanel.Component is Canvas) {
					if (item.HorizontalAlignment == HorizontalAlignment.Right) {
						item.DesignItem.Properties.GetAttachedProperty(Canvas.RightProperty).SetValue(xmax - item.Xmax);
					} else {
						item.DesignItem.Properties.GetAttachedProperty(Canvas.LeftProperty).SetValue(item.Xmin - xmin);
					}
					
					if (item.VerticalAlignment == VerticalAlignment.Bottom) {
						item.DesignItem.Properties.GetAttachedProperty(Canvas.BottomProperty).SetValue(ymax - item.Ymax);
					} else {
						item.DesignItem.Properties.GetAttachedProperty(Canvas.TopProperty).SetValue(item.Ymin - ymin);
					}

					newPanel.ContentProperty.CollectionElements.Add(item.DesignItem);

				} else if (newPanel.Component is Grid) {
					double tLeft = 0, tTop = 0, tRight = 0, tBottom = 0;
					if (item.HorizontalAlignment == HorizontalAlignment.Right) {
						item.DesignItem.Properties.GetProperty(Control.HorizontalAlignmentProperty).SetValue(HorizontalAlignment.Right);
						tRight = xmax - item.Xmax;
					} else {
						item.DesignItem.Properties.GetProperty(Control.HorizontalAlignmentProperty).SetValue(HorizontalAlignment.Left);
						tLeft = item.Xmin - xmin;
					}
					
					if (item.VerticalAlignment == VerticalAlignment.Bottom) {
						item.DesignItem.Properties.GetProperty(Control.VerticalAlignmentProperty).SetValue(VerticalAlignment.Bottom);
						tBottom = ymax - item.Ymax;
					} else {
						item.DesignItem.Properties.GetProperty(Control.VerticalAlignmentProperty).SetValue(VerticalAlignment.Top);
						tTop = item.Ymin - ymin;
					}
					
					item.DesignItem.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(tLeft, tTop, tRight, tBottom));

					newPanel.ContentProperty.CollectionElements.Add(item.DesignItem);

				} else if (newPanel.Component is Viewbox) {
					newPanel.ContentProperty.SetValue(item.DesignItem);
				}
				else if (newPanel.Component is ContentControl) {
					newPanel.ContentProperty.SetValue(item.DesignItem);
				}
			}

			if (doInsert)
			{
				PlacementOperation operation2 = PlacementOperation.TryStartInsertNewComponents(
					container,
					new[] {newPanel},
					new[] {new Rect(xmin, ymin, xmax - xmin, ymax - ymin).Round()},
					PlacementType.AddItem
				);

				if (items.Count() == 1 && container.ContentProperty != null && container.ContentProperty.IsCollection) {
					container.ContentProperty.CollectionElements.Remove(newPanel);
					container.ContentProperty.CollectionElements.Insert(firstIndex.Value, newPanel);
				}

				operation2.Commit();

				_context.Services.Selection.SetSelectedComponents(new[] {newPanel});
			}

			operation.Commit();

			return new Tuple<DesignItem, Rect>(newPanel, new Rect(xmin, ymin, xmax - xmin, ymax - ymin).Round());
		}

		public static void UnwrapItemsFromContainer(DesignItem container)
		{
			var collection = container.ContentProperty.CollectionElements.ToList();

			var newPanel = container.Parent;

			if (collection.Any(x => x.Parent != container))
				return;

			//Change Code to use the Placment Operation!
			var placement = container.Extensions.OfType<IPlacementBehavior>().FirstOrDefault();
			if (placement == null)
				return;

			var operation = PlacementOperation.Start(collection.ToList(), PlacementType.Move);

			List<ItemPos> itemList = new List<ItemPos>();

			int? firstIndex = null;

			var containerPos = GetItemPos(operation, container);

			foreach (var item in collection)
			{
				itemList.Add(GetItemPos(operation, item));
				if (container.Component is Canvas)
				{
					item.Properties.GetAttachedProperty(Canvas.RightProperty).Reset();
					item.Properties.GetAttachedProperty(Canvas.LeftProperty).Reset();
					item.Properties.GetAttachedProperty(Canvas.TopProperty).Reset();
					item.Properties.GetAttachedProperty(Canvas.BottomProperty).Reset();
				}
				else if (container.Component is Grid)
				{
					item.Properties.GetProperty(Control.HorizontalAlignmentProperty).Reset();
					item.Properties.GetProperty(Control.VerticalAlignmentProperty).Reset();
					item.Properties.GetProperty(Control.MarginProperty).Reset();
				}

				if (item.ParentProperty.IsCollection)
				{
					var parCol = item.ParentProperty.CollectionElements;
					if (!firstIndex.HasValue)
						firstIndex = parCol.IndexOf(item);
					parCol.Remove(item);
				}
				else
				{
					item.ParentProperty.Reset();
				}
			}

			newPanel.ContentProperty.CollectionElements.Remove(container);

			foreach (var item in itemList)
			{
				if (newPanel.Component is Canvas)
				{
					if (item.HorizontalAlignment == HorizontalAlignment.Right)
					{
						item.DesignItem.Properties.GetAttachedProperty(Canvas.RightProperty).SetValue(containerPos.Xmax - item.Xmax);
					}
					else
					{
						item.DesignItem.Properties.GetAttachedProperty(Canvas.LeftProperty).SetValue(item.Xmin + containerPos.Xmin);
					}

					if (item.VerticalAlignment == VerticalAlignment.Bottom)
					{
						item.DesignItem.Properties.GetAttachedProperty(Canvas.BottomProperty).SetValue(containerPos.Ymax - item.Ymax);
					}
					else
					{
						item.DesignItem.Properties.GetAttachedProperty(Canvas.TopProperty).SetValue(item.Ymin + containerPos.Ymin);
					}

					newPanel.ContentProperty.CollectionElements.Add(item.DesignItem);

				}
				else if (newPanel.Component is Grid)
				{
					double tLeft2 = 0, tTop2 = 0, tRight2 = 0, tBottom2 = 0;
					if (item.HorizontalAlignment == HorizontalAlignment.Right)
					{
						item.DesignItem.Properties.GetProperty(Control.HorizontalAlignmentProperty).SetValue(HorizontalAlignment.Right);
						tRight2 = containerPos.Xmax - item.Xmax;
					}
					else
					{
						item.DesignItem.Properties.GetProperty(Control.HorizontalAlignmentProperty).SetValue(HorizontalAlignment.Left);
						tLeft2 = item.Xmin;
					}

					if (item.VerticalAlignment == VerticalAlignment.Bottom)
					{
						item.DesignItem.Properties.GetProperty(Control.VerticalAlignmentProperty).SetValue(VerticalAlignment.Bottom);
						tBottom2 = containerPos.Ymax - item.Ymax;
					}
					else
					{
						item.DesignItem.Properties.GetProperty(Control.VerticalAlignmentProperty).SetValue(VerticalAlignment.Top);
						tTop2 = item.Ymin;
					}

					item.DesignItem.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(tLeft2, tTop2, tRight2, tBottom2));

					newPanel.ContentProperty.CollectionElements.Add(item.DesignItem);

				}
				else if (newPanel.Component is Viewbox)
				{
					newPanel.ContentProperty.SetValue(item.DesignItem);
				}
				else if (newPanel.Component is ContentControl)
				{
					newPanel.ContentProperty.SetValue(item.DesignItem);
				}
			}

			operation.Commit();
		}

		public static void ApplyTransform(DesignItem designItem, Transform transform, bool relative = true, AvaloniaProperty transformProperty = null)
		{
			var changeGroup = designItem.OpenGroup("Apply Transform");

			transformProperty = transformProperty ?? Control.RenderTransformProperty;
			Transform oldTransform = null;
			if (designItem.Properties.GetProperty(transformProperty).IsSet) {
				oldTransform = designItem.Properties.GetProperty(transformProperty).GetConvertedValueOnInstance<Transform>();
			}
			
			if (oldTransform is MatrixTransform) {
				var mt = oldTransform as MatrixTransform;
				var tg = new TransformGroup();
				if (mt.Matrix.M31 != 0 && mt.Matrix.M32 != 0)
					tg.Children.Add(new TranslateTransform(){ X = mt.Matrix.M31, Y = mt.Matrix.M32 });
				if (mt.Matrix.M11 != 0 && mt.Matrix.M22 != 0)
					tg.Children.Add(new ScaleTransform(){ ScaleX = mt.Matrix.M11, ScaleY = mt.Matrix.M22 });

				var angle = Math.Atan2(mt.Matrix.M21, mt.Matrix.M11) * 180 / Math.PI;
				if (angle != 0)
					tg.Children.Add(new RotateTransform(){ Angle = angle });
				//if (mt.Matrix.M11 != 0 && mt.Matrix.M22 != 0)
				//	tg.Children.Add(new SkewTransform(){ ScaleX = mt.Matrix.M11, ScaleY = mt.Matrix.M22 });
			} else if (oldTransform != null && oldTransform.GetType() != transform.GetType()) {
				var tg = new TransformGroup();
				var tgDes = designItem.Services.Component.RegisterComponentForDesigner(tg);
				tgDes.ContentProperty.CollectionElements.Add(designItem.Services.Component.GetDesignItem(oldTransform));
				designItem.Properties.GetProperty(Control.RenderTransformProperty).SetValue(tg);
				oldTransform = tg;
			}
			
			
			
			if (transform is RotateTransform) {
				var rotateTransform = transform as RotateTransform;
				
				if (oldTransform is RotateTransform || oldTransform == null) {
					if (rotateTransform.Angle != 0) {
						// Register the transform as a DesignItem first so we can set sub-properties
						var transformDesignItem = designItem.Services.Component.RegisterComponentForDesigner(rotateTransform);
						
						var angle = rotateTransform.Angle;
						if (relative && oldTransform != null) {
							angle = rotateTransform.Angle + ((RotateTransform)oldTransform).Angle;
						}
						// Set sub-properties on the DesignItem before assigning to parent
						transformDesignItem.Properties.GetProperty(RotateTransform.AngleProperty).SetValue(angle);
						if (rotateTransform.CenterX != 0.0)
							transformDesignItem.Properties.GetProperty(RotateTransform.CenterXProperty).SetValue(rotateTransform.CenterX);
						if (rotateTransform.CenterY != 0.0)
							transformDesignItem.Properties.GetProperty(RotateTransform.CenterYProperty).SetValue(rotateTransform.CenterY);

						// Now assign the fully configured transform to the property
						designItem.Properties.GetProperty(transformProperty).SetValue(transformDesignItem);

						if (oldTransform == null)
							designItem.Properties.GetProperty(Visual.RenderTransformOriginProperty).SetValue("50%,50%");
					}
					else {
						designItem.Properties.GetProperty(transformProperty).Reset();
						designItem.Properties.GetProperty(Visual.RenderTransformOriginProperty).Reset();
					}
				} else if (oldTransform is TransformGroup) {
					var tg = oldTransform as TransformGroup;
					var rot = tg.Children.FirstOrDefault(x=> x is RotateTransform);
					if  (rot != null) {
						designItem.Services.Component.GetDesignItem(tg).ContentProperty.CollectionElements.Remove(designItem.Services.Component.GetDesignItem(rot));
					}
					if (rotateTransform.Angle != 0) {
						var des = designItem.Services.Component.GetDesignItem(transform);
						if (des == null)
							des = designItem.Services.Component.RegisterComponentForDesigner(transform);
						designItem.Services.Component.GetDesignItem(tg).ContentProperty.CollectionElements.Add(des);
						if (oldTransform == null)
							designItem.Properties.GetProperty(Visual.RenderTransformOriginProperty).SetValue("50%,50%");
					}
				} else {
						if (rotateTransform.Angle != 0) {
							designItem.Properties.GetProperty(transformProperty).SetValue(transform);
							if (oldTransform == null)
								designItem.Properties.GetProperty(Visual.RenderTransformOriginProperty).SetValue("50%,50%");
					}
				}
			}
		
			((DesignPanel) designItem.Services.DesignPanel).AdornerLayer.UpdateAdornersForElement(designItem.View, true);
			
			changeGroup.Commit();
		}

		public static void StretchItems(IEnumerable<DesignItem> items, StretchDirection stretchDirection)
		{
			var collection = items;

			var container = collection.First().Parent;

			if (collection.Any(x => x.Parent != container))
				return;

			var placement = container.Extensions.OfType<IPlacementBehavior>().FirstOrDefault();
			if (placement == null)
				return;

			var changeGroup = container.OpenGroup("StretchItems");

			var w = GetWidth(collection.First().View);
			var h = GetHeight(collection.First().View);

			foreach (var item in collection.Skip(1))
			{
				switch (stretchDirection)
				{
					case StretchDirection.Width:
						{
							if (!double.IsNaN(w))
								item.Properties.GetProperty(Control.WidthProperty).SetValue(w);
						}
						break;
					case StretchDirection.Height:
						{
							if (!double.IsNaN(h))
								item.Properties.GetProperty(Control.HeightProperty).SetValue(h);
						}
						break;
				}
			}

			changeGroup.Commit();
		}

		public static void ArrangeItems(IEnumerable<DesignItem> items, ArrangeDirection arrangeDirection)
		{
			var collection = items;

			var _context = collection.First().Context as XamlDesignContext;

			var container = collection.First().Parent;

			if (collection.Any(x => x.Parent != container))
				return;

			var placement = container.Extensions.OfType<IPlacementBehavior>().FirstOrDefault();
			if (placement == null)
				return;

			var operation = PlacementOperation.Start(items.ToList(), PlacementType.Move);
			
			List<ItemPos> itemList = new List<ItemPos>();
			foreach (var item in collection)
			{
				itemList.Add(GetItemPos(operation, item));
			}

			var xmin = itemList.Min(x => x.Xmin);
			var xmax = itemList.Max(x => x.Xmax);
			var mpos = (xmax - xmin) / 2 + xmin;
			var ymin = itemList.Min(x => x.Ymin);
			var ymax = itemList.Max(x => x.Ymax);
			var ympos = (ymax - ymin) / 2 + ymin;

			foreach (var item in collection)
			{
				switch (arrangeDirection)
				{
					case ArrangeDirection.Left:
						{
							if (container.Component is Canvas)
							{
								if (!item.Properties.GetAttachedProperty(Canvas.RightProperty).IsSet)
								{
									item.Properties.GetAttachedProperty(Canvas.LeftProperty).SetValue(xmin);
								}
								else
								{
									var pos = (double)((Panel)item.Parent.Component).Bounds.Width - (xmin + (double) ((Control) item.Component).Bounds.Width);
									item.Properties.GetAttachedProperty(Canvas.RightProperty).SetValue(pos);
								}
							}
							else if (container.Component is Grid)
							{
								if (item.Properties.GetProperty(Control.HorizontalAlignmentProperty).GetConvertedValueOnInstance<HorizontalAlignment>() != HorizontalAlignment.Right)
								{
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(xmin, margin.Top, margin.Right, margin.Bottom));
								}
								else
								{
									var pos = (double)((Panel)item.Parent.Component).Bounds.Width - (xmin + (double)((Control)item.Component).Bounds.Width);
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(margin.Left, margin.Top, pos, margin.Bottom));
								}
							}
						}
						break;
					case ArrangeDirection.HorizontalMiddle:
						{
							if (container.Component is Canvas)
							{
								if (!item.Properties.GetAttachedProperty(Canvas.RightProperty).IsSet)
								{
									if (!item.Properties.GetAttachedProperty(Canvas.RightProperty).IsSet)
									{
										item.Properties.GetAttachedProperty(Canvas.LeftProperty).SetValue(mpos - (((Control)item.Component).Bounds.Width) / 2);
									}
									else
									{
										var pp = mpos - (((Control)item.Component).Bounds.Width) / 2;
										var pos = (double)((Panel)item.Parent.Component).Bounds.Width - pp - (((Control)item.Component).Bounds.Width);
										item.Properties.GetAttachedProperty(Canvas.RightProperty).SetValue(pos);
									}
								}
							}
							else if (container.Component is Grid)
							{
								if (item.Properties.GetProperty(Control.HorizontalAlignmentProperty).GetConvertedValueOnInstance<HorizontalAlignment>() != HorizontalAlignment.Right)
								{
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(mpos - (((Control)item.Component).Bounds.Width) / 2, margin.Top, margin.Right, margin.Bottom));
								}
								else
								{
									var pp = mpos - (((Control)item.Component).Bounds.Width) / 2;
									var pos = (double)((Panel)item.Parent.Component).Bounds.Width - pp - (((Control)item.Component).Bounds.Width);
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(margin.Left, margin.Top, pos, margin.Bottom));
								}
							}
						}
						break;
					case ArrangeDirection.Right:
						{
							if (container.Component is Canvas)
							{
								if (!item.Properties.GetAttachedProperty(Canvas.RightProperty).IsSet)
								{
									var pos = xmax - (double)((Control)item.Component).Bounds.Width;
									item.Properties.GetAttachedProperty(Canvas.LeftProperty).SetValue(pos);
								}
								else
								{
									var pos = (double)((Panel)item.Parent.Component).Bounds.Width - xmax;
									item.Properties.GetAttachedProperty(Canvas.RightProperty).SetValue(pos);
								}
							}
							else if (container.Component is Grid)
							{
								if (item.Properties.GetProperty(Control.HorizontalAlignmentProperty).GetConvertedValueOnInstance<HorizontalAlignment>() != HorizontalAlignment.Right)
								{
									var pos = xmax - (double)((Control)item.Component).Bounds.Width;
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(pos, margin.Top, margin.Right, margin.Bottom));
								}
								else
								{
									var pos = (double)((Panel)item.Parent.Component).Bounds.Width - xmax;
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(margin.Left, margin.Top, pos, margin.Bottom));
								}
							}
						}
						break;
					case ArrangeDirection.Top:
						{
							if (container.Component is Canvas)
							{
								if (!item.Properties.GetAttachedProperty(Canvas.BottomProperty).IsSet)
								{
									item.Properties.GetAttachedProperty(Canvas.TopProperty).SetValue(ymin);
								}
								else
								{
									var pos = (double)((Panel)item.Parent.Component).Bounds.Height - (ymin + (double)((Control)item.Component).Bounds.Height);
									item.Properties.GetAttachedProperty(Canvas.BottomProperty).SetValue(pos);
								}
							}
							else if (container.Component is Grid)
							{
								if (item.Properties.GetProperty(Control.VerticalAlignmentProperty).GetConvertedValueOnInstance<VerticalAlignment>() != VerticalAlignment.Bottom)
								{
									item.Properties.GetAttachedProperty(Canvas.TopProperty).SetValue(ymin);
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(margin.Left, ymin, margin.Right, margin.Bottom));
								}
								else
								{
									var pos = (double)((Panel)item.Parent.Component).Bounds.Height - (ymin + (double)((Control)item.Component).Bounds.Height);
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(margin.Left, margin.Top, margin.Right, pos));
								}
							}
						}
						break;
					case ArrangeDirection.VerticalMiddle:
						{
							if (container.Component is Canvas)
							{
								if (!item.Properties.GetAttachedProperty(Canvas.BottomProperty).IsSet)
								{
									item.Properties.GetAttachedProperty(Canvas.TopProperty).SetValue(ympos - (((Control)item.Component).Bounds.Height) / 2);
								}
								else
								{
									var pp = mpos - (((Control)item.Component).Bounds.Height) / 2;
									var pos = (double)((Panel)item.Parent.Component).Bounds.Height - pp - (((Control)item.Component).Bounds.Height);
									item.Properties.GetAttachedProperty(Canvas.BottomProperty).SetValue(pos);
								}
							}
							else if (container.Component is Grid)
							{
								if (item.Properties.GetProperty(Control.VerticalAlignmentProperty).GetConvertedValueOnInstance<VerticalAlignment>() != VerticalAlignment.Bottom)
								{
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(margin.Left, ympos - (((Control)item.Component).Bounds.Height) / 2, margin.Right, margin.Bottom));
								}
								else
								{
									var pp = mpos - (((Control)item.Component).Bounds.Height) / 2;
									var pos = (double)((Panel)item.Parent.Component).Bounds.Height - pp - (((Control)item.Component).Bounds.Height);
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(margin.Left, margin.Top, margin.Right, pos));
								}
							}
						}
						break;
					case ArrangeDirection.Bottom:
						{
							if (container.Component is Canvas)
							{
								if (!item.Properties.GetAttachedProperty(Canvas.BottomProperty).IsSet)
								{
									var pos = ymax - (double)((Control)item.Component).Bounds.Height;
									item.Properties.GetAttachedProperty(Canvas.TopProperty).SetValue(pos);
								}
								else
								{
									var pos = (double)((Panel)item.Parent.Component).Bounds.Height - ymax;
									item.Properties.GetAttachedProperty(Canvas.BottomProperty).SetValue(pos);
								}
							}
							else if (container.Component is Grid)
							{
								if (item.Properties.GetProperty(Control.VerticalAlignmentProperty).GetConvertedValueOnInstance<VerticalAlignment>() != VerticalAlignment.Bottom)
								{
									var pos = ymax - (double)((Control)item.Component).Bounds.Height;
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(margin.Left, pos, margin.Right, margin.Bottom));
								}
								else
								{
									var pos = (double)((Panel)item.Parent.Component).Bounds.Height - ymax;
									var margin = item.Properties.GetProperty(Control.MarginProperty).GetConvertedValueOnInstance<Thickness>();
									item.Properties.GetProperty(Control.MarginProperty).SetValue(new Thickness(margin.Left, margin.Top, margin.Right, pos));
								}
							}
						}
						break;
				}
			}

			operation.Commit();
		}
		
//		public static class Path {
//			
//			public static PathGeometry ConvertToPathGeometry(this TextBlock textBlock)
//			{
//				//var ft = new FormatedText();
//				return null;
//			}
//			
//			public static PathGeometry ConvertToPathGeometry(this Rectangle rectangle)
//			{
//				return null;
//			}
//			
//			public static PathGeometry ConvertToPathGeometry(this Ellipse ellipse)
//			{
//				return null;
//			}
//		}
	}
}
