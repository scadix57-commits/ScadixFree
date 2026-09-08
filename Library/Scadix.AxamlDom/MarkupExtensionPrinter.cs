

using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Portable.Xaml.Markup;
using System.Text;


namespace Scadix.AxamlDom
{
	/// <summary>
	/// Static class that can generate XAML markup extension code ("{Binding Path=...}").
	/// </summary>
	public static class MarkupExtensionPrinter
	{
		/// <summary>
		/// Gets whether shorthand XAML markup extension code can be generated for the object.
		/// </summary>
		public static bool CanPrint(XamlObject obj)
		{
			if (obj.ElementType == typeof(MultiBinding) ||
			    obj.ElementType == typeof(BindingPriority)) {
				return false;
			}
			
			return CanPrint(obj, false, GetNonMarkupExtensionParent(obj));
		}
		
		/// <summary>
		/// Generates XAML markup extension code for the object.
		/// </summary>
		public static string Print(XamlObject obj)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("{");
			sb.Append(obj.GetNameForMarkupExtension());

			bool first = true;
			var properties = obj.Properties.ToList();
			
			if (obj.ElementType == typeof(Binding)){
				var p=obj.Properties.FirstOrDefault(x=>x.PropertyName=="Path");
				if (p!=null && p.IsSet) {
					sb.Append(" ");
					AppendPropertyValue(sb, p.PropertyValue, false);
					properties.Remove(p);
					first = false;
				}
			}
			else if (obj.ElementType == typeof(Reference)){
				var p=obj.Properties.FirstOrDefault(x=>x.PropertyName=="Name");
				if (p!=null && p.IsSet) {
					sb.Append(" ");
					AppendPropertyValue(sb, p.PropertyValue, false);
					properties.Remove(p);
					first = false;
				}
			}
			else if (obj.ElementType == typeof(StaticResourceExtension)){
				var p=obj.Properties.FirstOrDefault(x=>x.PropertyName=="ResourceKey");
				if (p!=null && p.IsSet) {
					sb.Append(" ");
					AppendPropertyValue(sb, p.PropertyValue, false);
					properties.Remove(p);
					first = false;
				}
			}
			
			foreach (var property in properties) {
				if (!property.IsSet) continue;

				if (first)
					sb.Append(" ");
				else
					sb.Append(", ");
				first = false;

				sb.Append(property.GetNameForMarkupExtension());
				sb.Append("=");

				AppendPropertyValue(sb, property.PropertyValue, property.ReturnType == typeof(string));
			}
			sb.Append("}");
			return sb.ToString();
		}
		
		private static void AppendPropertyValue(StringBuilder sb, XamlPropertyValue value, bool isStringProperty)
		{
			var textValue = value as XamlTextValue;
			if (textValue != null) {
				string text = textValue.Text;
				bool containsSpace = text.Contains(' ');
				
				if(containsSpace) {
					sb.Append('\'');
				}
				
				if (isStringProperty)
					sb.Append(text.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}"));
				else
					sb.Append(text.Replace("\\", "\\\\"));
				
				if(containsSpace) {
					sb.Append('\'');
				}
			} else if (value is XamlObject) {
				sb.Append(Print(value as XamlObject));
			}
		}
		
		private static bool CanPrint(XamlObject obj, bool isNested, XamlObject nonMarkupExtensionParent)
		{
			if ((isNested || obj.ParentObject == nonMarkupExtensionParent) && IsStaticResourceThatReferencesLocalResource(obj, nonMarkupExtensionParent)) {
				return false;
			}
			
			foreach (var property in obj.Properties.Where((prop) => prop.IsSet)) {
				var value = property.PropertyValue;
				if (value is XamlTextValue)
					continue;
				else {
					var xamlObject = value as XamlObject;
					if (xamlObject == null || !xamlObject.IsMarkupExtension)
						return false;
					else if (!CanPrint(xamlObject, true, nonMarkupExtensionParent))
						return false;
				}
			}

			return true;
		}
		
		private static XamlObject GetNonMarkupExtensionParent(XamlObject markupExtensionObject)
		{
			System.Diagnostics.Debug.Assert(markupExtensionObject.IsMarkupExtension);
			
			XamlObject obj = markupExtensionObject;
			while (obj != null && obj.IsMarkupExtension) {
				obj = obj.ParentObject;
			}
			return obj;
		}
		
		private static bool IsStaticResourceThatReferencesLocalResource(XamlObject obj, XamlObject nonMarkupExtensionParent)
		{
			var staticResource = obj.Instance as StaticResourceExtension;
			if (staticResource != null && staticResource.ResourceKey != null && nonMarkupExtensionParent != null) {

				var parentLocalResource = nonMarkupExtensionParent.ServiceProvider.Resolver.FindLocalResource(staticResource.ResourceKey);
				
				// If resource with the specified key is declared locally on the same object as the StaticResource is being used the markup extension
				// must be printed as element to find the resource, otherwise it will search from parent-parent and find none or another resource.
				if (parentLocalResource != null)
					return true;
			}
			
			return false;
		}
	}
}
