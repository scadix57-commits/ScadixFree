namespace Scadix.AxamlDesign.Extensions
{
	/// <summary>
	/// Attribute to specify Properties of the Extension.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=false)]
	public sealed class ExtensionAttribute : Attribute
	{
		/// <summary>
		/// Gets or sets the Order in wich the extensions are used.
		/// </summary>
		public int Order { get; set; }
	}
}
