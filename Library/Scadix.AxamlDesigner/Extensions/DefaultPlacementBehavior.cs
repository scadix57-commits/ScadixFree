 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDom;

namespace Scadix.AxamlDesigner.Extensions
{
	[ExtensionFor(typeof(Panel))]
	[ExtensionFor(typeof(Control))]
	[ExtensionFor(typeof(Border))]
	[ExtensionFor(typeof(Viewbox))]
	public class DefaultPlacementBehavior : BehaviorExtension, IPlacementBehavior
	{
		static DefaultPlacementBehavior()
		{ }

		protected override void OnInitialized()
		{
			base.OnInitialized();
			if (ExtendedItem.ContentProperty == null ||
				Metadata.IsPlacementDisabled(ExtendedItem.ComponentType))
				return;
			ExtendedItem.AddBehavior(typeof(IPlacementBehavior), this);
		}

		public virtual bool CanPlace(IEnumerable<DesignItem> childItems, PlacementType type, PlacementAlignment position)
		{
			return true;
		}

		public virtual void BeginPlacement(PlacementOperation operation)
		{
		}

		public virtual void EndPlacement(PlacementOperation operation)
		{
			InfoTextEnterArea.Stop(ref infoTextEnterArea);
		}

		public virtual Rect GetPosition(PlacementOperation operation, DesignItem item)
		{
			return GetPositionRelativeToContainer(operation, item);
		}

		public Rect GetPositionRelativeToContainer(PlacementOperation operation, DesignItem item)
		{
			if (item.View == null)
				return default(Rect);
			var p = item.View.TranslatePoint(new Point(), operation.CurrentContainer.View);

			return new Rect(p ?? new Point(), PlacementOperation.GetRealElementSize(item.View));
		}

		public virtual void BeforeSetPosition(PlacementOperation operation)
		{
		}

		public virtual bool CanPlaceItem(PlacementInformation info)
		{
			return true;
		}

		public virtual void SetPosition(PlacementInformation info)
		{
			if (info.Operation.Type != PlacementType.Move
				&& info.Operation.Type != PlacementType.MovePoint
				&& info.Operation.Type != PlacementType.MoveAndIgnoreOtherContainers)
				ModelTools.Resize(info.Item, info.Bounds.Width, info.Bounds.Height);

			//if (info.Operation.Type == PlacementType.MovePoint)
			//	ModelTools.Resize(info.Item, info.Bounds.Width, info.Bounds.Height);
		}

		public virtual bool CanLeaveContainer(PlacementOperation operation)
		{
			return true;
		}

		public virtual void LeaveContainer(PlacementOperation operation)
		{
			foreach (var info in operation.PlacedItems) {
				var parentProperty = info.Item.ParentProperty;
				if (parentProperty.IsCollection)
				{
					parentProperty.CollectionElements.Remove(info.Item);
				}
				else
				{
					parentProperty.Reset();
				}
			}
		}

		private static InfoTextEnterArea infoTextEnterArea;

		public virtual bool CanEnterContainer(PlacementOperation operation, bool shouldAlwaysEnter)
		{
			var canEnter = internalCanEnterContainer(operation);

			if (canEnter && !shouldAlwaysEnter)
			{
				var b = new Rect(0, 0, ((Control)this.ExtendedItem.View).Bounds.Width, ((Control)this.ExtendedItem.View).Bounds.Height);
				InfoTextEnterArea.Start(ref infoTextEnterArea, this.Services, this.ExtendedItem.View, b, Translations.Instance.PressAltText);

				return false;
			}

			return canEnter;
		}

		private bool internalCanEnterContainer(PlacementOperation operation)
		{
			InfoTextEnterArea.Stop(ref infoTextEnterArea);

			if (ExtendedItem.Component is Expander)
			{
				if (!((Expander)ExtendedItem.Component).IsExpanded)
				{
					((Expander)ExtendedItem.Component).IsExpanded = true;
				}
			}

			if (ExtendedItem.Component is UserControl && ExtendedItem.ComponentType != typeof(UserControl))
				return false;

			if (ExtendedItem.Component is Decorator)
				return ((Decorator)ExtendedItem.Component).Child == null;

			if (ExtendedItem.ContentProperty.IsCollection)
				return CollectionSupport.CanCollectionAdd(ExtendedItem.ContentProperty.ReturnType,
														  operation.PlacedItems.Select(p => p.Item.Component));

			if (ExtendedItem.ContentProperty.ReturnType == typeof(string))
				return false;

			if (!ExtendedItem.ContentProperty.IsSet)
				return true;

			object value = ExtendedItem.ContentProperty.GetConvertedValueOnInstance<object>();
			// don't overwrite non-primitive values like bindings
			return ExtendedItem.ContentProperty.Value == null && (value is string && string.IsNullOrEmpty(value as string));
		}

		public virtual void EnterContainer(PlacementOperation operation)
		{
			if (ExtendedItem.ContentProperty.IsCollection)
			{
				foreach (var info in operation.PlacedItems)
				{
					ExtendedItem.ContentProperty.CollectionElements.Add(info.Item);
				}
			}
			else
			{
				ExtendedItem.ContentProperty.SetValue(operation.PlacedItems[0].Item);
			}
			if (operation.Type == PlacementType.AddItem)
			{
				foreach (var info in operation.PlacedItems)
				{
					SetPosition(info);
				}
			}
		}

		public virtual Point PlacePoint(Point point)
		{
			return new Point(Math.Round(point.X), Math.Round(point.Y));
		}
	}
}
