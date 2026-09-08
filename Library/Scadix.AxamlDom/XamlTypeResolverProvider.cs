

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System.Xml;


namespace Scadix.AxamlDom
{
	sealed class XamlTypeResolverProvider : IXamlTypeResolver, IServiceProvider
	{
		XamlDocument document;
		XamlObject containingObject;
		
		public XamlTypeResolverProvider(XamlObject containingObject)
		{
			if (containingObject == null)
				throw new ArgumentNullException("containingObject");
			this.document = containingObject.OwnerDocument;
			this.containingObject = containingObject;
		}

		XmlElement ContainingElement{
			get { return containingObject.XmlElement; }
		}

		private string GetNamespaceOfPrefix(string prefix)
		{
			var ns = ContainingElement.GetNamespaceOfPrefix(prefix);
			if (!string.IsNullOrEmpty(ns))
				return ns;
			var obj = containingObject;
			while (obj != null)
			{
				ns = obj.XmlElement.GetNamespaceOfPrefix(prefix);
				if (!string.IsNullOrEmpty(ns))
					return ns;
				obj = obj.ParentObject;
			}
			return null;
		}
		
		public Type Resolve(string typeName)
		{
			string typeNamespaceUri;
			string typeLocalName;
			if (typeName.Contains(":")) {
				typeNamespaceUri = GetNamespaceOfPrefix(typeName.Substring(0, typeName.IndexOf(':')));
				typeLocalName = typeName.Substring(typeName.IndexOf(':') + 1);
			} else {
				typeNamespaceUri = GetNamespaceOfPrefix("");
				typeLocalName = typeName;
			}
			if (string.IsNullOrEmpty(typeNamespaceUri)) {
				var documentResolver = this.document.RootElement.ServiceProvider.Resolver;
				if (documentResolver != null && documentResolver != this) {
					return documentResolver.Resolve(typeName);
				}
				
				throw new XamlMarkupExtensionParseException("Unrecognized namespace prefix in type " + typeName);
			}
			return document.TypeFinder.GetType(typeNamespaceUri, typeLocalName);
		}
		
		public object GetService(Type serviceType)
		{
			if (serviceType == typeof(IXamlTypeResolver) || serviceType == typeof(XamlTypeResolverProvider))
				return this;
			else
				return document.ServiceProvider.GetService(serviceType);
		}
		
		public XamlPropertyInfo ResolveProperty(string propertyName)
		{
            string propertyNamespace;
            if (propertyName.Contains(":"))
            {
                propertyNamespace = ContainingElement.GetNamespaceOfPrefix(propertyName.Substring(0, propertyName.IndexOf(':')));
                propertyName = propertyName.Substring(propertyName.IndexOf(':') + 1);
            }
            else propertyNamespace = ContainingElement.GetNamespaceOfPrefix(string.Empty);

            var elementType = ResolveTargetElementType();

            if (propertyName.Contains("."))
            {
                var allPropertiesAllowed = containingObject is XamlObject && (containingObject.ElementType == typeof(Setter) || containingObject.IsMarkupExtension);
                return XamlParser.GetPropertyInfo(document.TypeFinder, null, elementType, propertyNamespace, propertyName, allPropertiesAllowed);
            }

            if (elementType != null) return XamlParser.FindProperty(null, elementType, propertyName);
            return null;
        }
		
		public object FindResource(object key)
		{
			XamlObject obj = containingObject;
			while (obj != null) {
                Control el = obj.Instance as Control;
				if (el != null) {
					object val = el.Resources[key];
					if (val != null)
						return val;
				}
				obj = obj.ParentObject;
			}
			return null;
		}
		
		public object FindLocalResource(object key)
		{
            Control el = containingObject.Instance as Control;
			if (el != null) {
				return el.Resources[key];
			}
			return null;
		}

        private Type ResolveTargetElementType()
        {
            Type elementType = null;
            var obj = containingObject;

            // 1. ControlTheme.TargetType
            var tempObj = obj;
            while (tempObj != null)
            {
                if (tempObj.Instance is Avalonia.Styling.ControlTheme theme && theme.TargetType != null)
                {
                    elementType = theme.TargetType;
                    break;
                }
                tempObj = tempObj.ParentObject;
            }

            // 2. Style selector target type
            if (elementType == null)
            {
                tempObj = obj;
                while (tempObj != null)
                {
                    if (tempObj.Instance is Avalonia.Styling.Style style && style.Selector != null)
                    {
                        var selectorString = style.Selector.ToString();
                        if (selectorString != null && !selectorString.StartsWith(":") && !selectorString.StartsWith("Nesting"))
                        {
                            var typeName = selectorString.Split(new[] { '.', ':', '#', '[', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                            if (typeName.StartsWith("OfType("))
                            {
                                typeName = typeName.Substring("OfType(".Length);
                                var endParen = typeName.LastIndexOf(')');
                                if (endParen > 0) typeName = typeName.Substring(0, endParen);
                            }
                            elementType = document.TypeFinder.GetType(XamlConstants.PresentationNamespace, typeName);
                        }
                        if (elementType != null) break;
                    }
                    tempObj = tempObj.ParentObject;
                }
            }

            // 3. Direct parent control
            if (elementType == null)
            {
                tempObj = obj;
                while (tempObj != null)
                {
                    if (tempObj.Instance is AvaloniaObject &&
                        tempObj.Instance is not Avalonia.Animation.Transitions &&
                        tempObj.Instance is not Avalonia.Animation.ITransition &&
                        tempObj.Instance is not Avalonia.Styling.Setter &&
                        tempObj.Instance is not Avalonia.Animation.KeyFrame &&
                        tempObj.Instance is not Avalonia.Animation.Animation &&
                        tempObj.Instance is not Avalonia.Controls.ResourceDictionary)
                    {
                        elementType = tempObj.Instance.GetType();
                        break;
                    }
                    tempObj = tempObj.ParentObject;
                }
            }
            return elementType;
        }
    }
}
