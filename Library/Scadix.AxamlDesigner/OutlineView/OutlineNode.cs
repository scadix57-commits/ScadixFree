

using System.Collections.Specialized;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Interfaces;

namespace Scadix.AxamlDesigner.OutlineView
{ 
	public class OutlineNode: OutlineNodeBase
	{
		protected OutlineNode(DesignItem designitem): base(designitem)
		{
			UpdateChildren();
			SelectionService.SelectionChanged += new EventHandler<DesignItemCollectionEventArgs>(Selection_SelectionChanged);
		}

		protected OutlineNode(string name) : base(name)
		{
		}

		static OutlineNode()
		{
			DummyPlacementType = PlacementType.Register("DummyPlacement");
		}

		[Obsolete("prefer using DesignItem.CreateOutlineNode()")]
		public static IOutlineNode Create(DesignItem designItem) => designItem.CreateOutlineNode();

		void Selection_SelectionChanged(object sender, DesignItemCollectionEventArgs e)
		{
			IsSelected = DesignItem.Services.Selection.IsComponentSelected(DesignItem);
		}

		protected override void UpdateChildren()
		{
			Children.Clear();

			foreach (var prp in DesignItem.AllSetProperties) {
				if (prp.Name != DesignItem.ContentPropertyName) 
				{
					if (prp.Value != null) {
						var propertyNode = PropertyOutlineNode.Create(prp);
						var node = prp.Value.CreateOutlineNode();
						propertyNode.Children.Add(node);
						Children.Add(propertyNode);
					}
				}
			}
			if (DesignItem.ContentPropertyName != null) {
				var content = DesignItem.ContentProperty;
				if (content.IsCollection) {
					UpdateChildrenCore(content.CollectionElements);
				} else {
					if (content.Value != null) {
						if (!UpdateChildrenCore(new[] {content.Value})) {
							var propertyNode = PropertyOutlineNode.Create(content);
							var node = content.Value.CreateOutlineNode();
							propertyNode.Children.Add(node);
							Children.Add(propertyNode);
						}
					}
				}
			}
		}

		protected override void UpdateChildrenCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Remove) {
				foreach (var oldItem in e.OldItems) {
					var item = Children.FirstOrDefault(x => x.DesignItem == oldItem);
					if (item != null) {
						Children.Remove(item);
					}
				}
			} else if (e.Action == NotifyCollectionChangedAction.Add) {
				UpdateChildrenCore(e.NewItems.Cast<DesignItem>(), e.NewStartingIndex);				
			}
		}

		bool UpdateChildrenCore(IEnumerable<DesignItem> items, int index = -1)
		{
			var retVal = false;
			foreach (var item in items) {
				if (ModelTools.CanSelectComponent(item)) {
					if (Children.All(x => x.DesignItem != item)) {
						var node = item.CreateOutlineNode();
						if (index > -1) {
							Children.Insert(index++, node);
							retVal = true;
						}
						else {
							Children.Add(node);
							retVal = true;
						}
					}
				} else {
					var content = item.ContentProperty;
					if (content != null) {
						if (content.IsCollection) {
							UpdateChildrenCore(content.CollectionElements);
							retVal = true;
						} else {
							if (content.Value != null) {
								UpdateChildrenCore(new[] { content.Value });
								retVal = true;
							}
						}
					}
				}
			}

			return retVal;
		}

		internal class OutlineNodeService : IOutlineNodeService, IDisposable
		{
			readonly Dictionary<DesignItem, IOutlineNode> outlineNodes = new Dictionary<DesignItem, IOutlineNode>();

			public IOutlineNode Create(DesignItem designItem)
			{
				IOutlineNode node = null;
				if (designItem != null && !outlineNodes.TryGetValue(designItem, out node))
				{
					node = new OutlineNode(designItem);
					outlineNodes[designItem] = node;
				}

				return node;
			}

			public void Dispose()
			{
				outlineNodes.Clear();
			}
		}
	}
}
