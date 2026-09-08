
using Avalonia.Controls;
using Portable.Xaml;

namespace Scadix.AxamlDesign.Extensions
{
	/// <summary>
	/// 
	/// </summary>
	[ExtensionServer(typeof(NeverApplyExtensionsExtensionServer))]
	public class XamlInstanceFactory : Extension
	{
		/// <summary>
		/// Gets a default instance factory that uses Activator.CreateInstance to create instances.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
		public static readonly XamlInstanceFactory DefaultInstanceFactory = new XamlInstanceFactory();
		
		/// <summary>
		/// Creates a new CustomInstanceFactory instance.
		/// </summary>
		protected XamlInstanceFactory()
		{
		}

		/// <summary>
		/// A Instance Factory that uses XAML to instanciate the Control!
		/// So you can add the 
		/// </summary>
		public virtual object CreateInstance(Type type, params object[] arguments)
		{
			var txt = @"<ContentControl xmlns=""https://github.com/avaloniaui"">
<ContentControl.ResourceDictionary>
<ResourceDictionary>
<ResourceDictionary.MergedDictionarys>
</ResourceDictionary.MergedDictionarys>
</ResourceDictionary>
</ContentControl.ResourceDictionary>
<a:{0} xmlns:a=""clr-namespace:{1};assembly={2}"" /></ContentControl>";

			var xaml = string.Format(txt, type.Name, type.Namespace, type.Assembly.GetName().Name);
			var contentControl = XamlServices.Load(new XamlXmlReader(new StringReader(xaml))) as ContentControl;

			return contentControl.Content;

		}
	}
}
