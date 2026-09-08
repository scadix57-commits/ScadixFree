namespace Scadix.AxamlDesign.Extensions
{
	/// <summary>
	/// Base class for extensions that provide a behavior interface for the designed item.
	/// These extensions are always loaded. They must have an parameter-less constructor.
	/// </summary>
	[ExtensionServer(typeof(DefaultExtensionServer.Permanent))]
	public class BehaviorExtension : DefaultExtension
	{ }
}
