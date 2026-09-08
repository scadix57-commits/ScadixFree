
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Templates;
using Portable.Xaml;
using System.Xml;

namespace Scadix.AxamlDom
{
	/// <summary>
	/// Contains template related helper methods.
	/// </summary>
	public static class TemplateHelper
	{
        /// <summary>
        /// Gets a <see cref="ControlTemplate"/> based on the specified parameters.
        /// </summary>
        /// <param name="xmlElement">The xml element to get template xaml from.</param>
        /// <param name="parentObject">The <see cref="XamlObject"/> to use as source for resources and contextual information.</param>
        /// <returns>A <see cref="ControlTemplate"/> based on the specified parameters.</returns>
        public static ControlTemplate GetFrameworkTemplate(XmlElement xmlElement, XamlObject parentObject)
		{

			var nav = xmlElement.CreateNavigator();

			var ns = new Dictionary<string, string>();
			while (true)
			{
				var nsInScope = nav.GetNamespacesInScope(XmlNamespaceScope.ExcludeXml);
				foreach (var ak in nsInScope)
				{
					if (!ns.ContainsKey(ak.Key) && ak.Key != "")
						ns.Add(ak.Key, ak.Value);
				}
				if (!nav.MoveToParent())
					break;
			}
			
			xmlElement = (XmlElement)xmlElement.CloneNode(true);
				
			foreach (var dictentry in ns.ToList())
			{
				var value = dictentry.Value;
				if (value.StartsWith("clr-namespace") && !value.Contains(";assembly=")) {
					if (!string.IsNullOrEmpty(parentObject.OwnerDocument.CurrentProjectAssemblyName)) {
						value += ";assembly=" + parentObject.OwnerDocument.CurrentProjectAssemblyName;
					}
				}
				xmlElement.SetAttribute("xmlns:" + dictentry.Key, value);
			}

			var keyAttrib = xmlElement.GetAttribute("Key", XamlConstants.XamlNamespace);

			if (string.IsNullOrEmpty(keyAttrib)) {
				xmlElement.SetAttribute("Key", XamlConstants.XamlNamespace, "$$temp&&§§%%__");
			}

			var xaml = xmlElement.OuterXml;
			xaml = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/netfx/2007/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" + xaml + "</ResourceDictionary>";
			StringReader stringReader = new StringReader(xaml);
			XmlReader xmlReader = XmlReader.Create(stringReader);
			var xamlReader = new XamlXmlReader(xmlReader, parentObject.ServiceProvider.SchemaContext);

			var seti = new XamlObjectWriterSettings();

			var resourceDictionary = new ResourceDictionary();
			var obj = parentObject;
			while (obj != null)
			{
				if (obj.Instance is ResourceDictionary)
				{
					var r = obj.Instance as ResourceDictionary;
					foreach (var k in r.Keys)
					{
						if (!resourceDictionary.ContainsKey(k))
							resourceDictionary.Add(k, r[k]);
					}
				}
				else if (obj.Instance is Control)
				{
					var r = ((Control)obj.Instance).Resources;
					foreach (var k in r.Keys)
					{
						if (!resourceDictionary.ContainsKey(k))
							resourceDictionary.Add(k, r[k]);
					}
				}

				obj = obj.ParentObject;
			}

			seti.BeforePropertiesHandler = (s, e) =>
			{
				if (seti.BeforePropertiesHandler != null)
				{
					var rr = e.Instance as ResourceDictionary;
					rr.MergedDictionaries.Add(resourceDictionary);
					seti.BeforePropertiesHandler = null;
				}
			};

			var writer = new XamlObjectWriter(parentObject.ServiceProvider.SchemaContext, seti);

			XamlServices.Transform(xamlReader, writer);

			var result = (ResourceDictionary)writer.Result;

			var enr = result.Keys.GetEnumerator();
			enr.MoveNext();
			var rdKey = enr.Current;

			var template = result[rdKey] as ControlTemplate;
			
			result.Remove(rdKey);
			return template;
		}

		
		private static Stream GenerateStreamFromString(string s)
		{
			MemoryStream stream = new MemoryStream();
			StreamWriter writer = new StreamWriter(stream);
			writer.Write(s);
			writer.Flush();
			stream.Position = 0;
			return stream;
		}
	}
}
