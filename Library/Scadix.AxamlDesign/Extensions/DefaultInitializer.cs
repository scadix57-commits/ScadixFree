namespace Scadix.AxamlDesign.Extensions
{
	/// <summary>
	/// Base class for extensions that initialize new controls with default values.
	/// </summary>
	[ExtensionServer(typeof(NeverApplyExtensionsExtensionServer))]
	public abstract class DefaultInitializer : Extension
	{
		/// <summary>
		/// Initializes the design item to default values.
		/// </summary>
		public abstract void InitializeDefaults(DesignItem item);
	}
}
