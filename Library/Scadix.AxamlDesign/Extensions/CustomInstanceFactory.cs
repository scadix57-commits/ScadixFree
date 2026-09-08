
using Avalonia.Controls;

namespace Scadix.AxamlDesign.Extensions
{
	/// <summary>
	/// A special kind of extension that is used to create instances of objects when loading XAML inside
	/// the designer.
	/// </summary>
	/// <remarks>
	/// CustomInstanceFactory in Cider: http://blogs.msdn.com/jnak/archive/2006/04/10/572241.aspx
	/// </remarks>
	[ExtensionServer(typeof(NeverApplyExtensionsExtensionServer))]
	public class CustomInstanceFactory : Extension
	{
		/// <summary>
		/// Gets a default instance factory that uses Activator.CreateInstance to create instances.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
		public static readonly CustomInstanceFactory DefaultInstanceFactory = new CustomInstanceFactory();
		
		/// <summary>
		/// Creates a new CustomInstanceFactory instance.
		/// </summary>
		protected CustomInstanceFactory()
		{
		}
		
		/// <summary>
		/// Creates an instance of the specified type, passing the specified arguments to its constructor.
		/// </summary>
		public virtual object CreateInstance(Type type, params object[] arguments)
		{
			var instance = Activator.CreateInstance(type, arguments);
			var uiElement = instance as Control;
			//if (uiElement != null)
			//	DesignerProperties.SetIsInDesignMode(uiElement, true);
			return instance;
		}
	}
}
