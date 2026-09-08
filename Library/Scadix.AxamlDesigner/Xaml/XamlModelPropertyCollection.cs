 

using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.Xaml
{
	sealed class XamlModelPropertyCollection : DesignItemPropertyCollection
	{
		XamlDesignItem _item;
		Dictionary<string, XamlModelProperty> propertiesDictionary = new Dictionary<string, XamlModelProperty>();
		
		public XamlModelPropertyCollection(XamlDesignItem item)
		{
			this._item = item;
		}
		
		public override DesignItemProperty GetProperty(string name)
		{
			XamlModelProperty property;
			if (propertiesDictionary.TryGetValue(name, out property))
				return property;
			property = new XamlModelProperty(_item, _item.XamlObject.FindOrCreateProperty(name));
			propertiesDictionary.Add(name, property);
			return property;
		}
		
		public override DesignItemProperty GetAttachedProperty(Type ownerType, string name)
		{
			return new XamlModelProperty(_item, _item.XamlObject.FindOrCreateAttachedProperty(ownerType, name));
		}
		
		public override System.Collections.Generic.IEnumerator<DesignItemProperty> GetEnumerator()
		{
			foreach (var value in propertiesDictionary.Values)
			{
				yield return value;
			}
		}
	}
}
