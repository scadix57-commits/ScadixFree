 
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Interfaces;

namespace Scadix.AxamlDesigner.OutlineView
{ 
	public class PropertyOutlineNode : OutlineNode
	{
		private DesignItemProperty _property;

		protected PropertyOutlineNode(DesignItemProperty property) : base(property.Name)
		{
			_property = property;
		}

		public override ServiceContainer Services
		{
			get { return this._property.DesignItem.Services; }
		}

		static PropertyOutlineNode()
		{
			DummyPlacementType = PlacementType.Register("DummyPlacement");
		}

		public static IOutlineNode Create(DesignItemProperty property)
		{
			return new PropertyOutlineNode(property);
		}
	}
}
