namespace Scadix.AxamlDesign.Extensions
{
	/// <summary>
	/// Instead of "DefaultInitializer" wich is only called for new objects, 
	/// this Initializer is called for every Instance of a Design Item
	/// </summary>
	[ExtensionServer(typeof(NeverApplyExtensionsExtensionServer))]
    public abstract class DesignItemInitializer : Extension
	{
		/// <summary>
		/// Initializes the design item.
		/// </summary>
		public abstract void InitializeDesignItem(DesignItem item);
	}
}