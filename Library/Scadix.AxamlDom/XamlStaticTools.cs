using System.Xml;

namespace Scadix.AxamlDom
{
	/// <summary>
	/// Static methods to help with designer operations which require access to internal Xaml elements.
	/// </summary>
	public static class XamlStaticTools
	{
		/// <summary>
		/// Gets the Xaml string of the <paramref name="xamlObject"/>
		/// </summary>
		/// <param name="xamlObject">The object whose Xaml is requested.</param>
		public static string GetXaml(XamlObject xamlObject)
		{
			if (xamlObject != null)
			{
				var nd = xamlObject.XmlElement.CloneNode(true);
				var attLst = nd.Attributes.Cast<XmlAttribute>().ToDictionary(x => x.Name);

				var ns = new List<XmlAttribute>();

				var parentObject = xamlObject.ParentObject;
				while (parentObject != null)
				{
					foreach (XmlAttribute attribute in parentObject.XmlElement.Attributes)
					{
						if (attribute.Name.StartsWith("xmlns:"))
						{
							var existingNs = nd.GetNamespaceOfPrefix(attribute.Name.Substring(6));
							if (string.IsNullOrEmpty(existingNs))
							{
								if (!attLst.ContainsKey(attribute.Name))
								{
									nd.Attributes.Append((XmlAttribute)attribute.CloneNode(false));
									attLst.Add(attribute.Name, attribute);
								}
							}
						}
					}
					parentObject = parentObject.ParentObject;
				}


				return nd.OuterXml;
			}
			return null;
		}
	}
}
