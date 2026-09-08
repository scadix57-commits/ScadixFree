namespace Scadix.AxamlDesign.Extensions
{
	/// <summary>
	/// Attribute to specify that the decorated class is a WPF extension for the specified item type.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple=true, Inherited=false)]
	public sealed class ExtensionForAttribute : Attribute
	{
		Type _designedItemType;
		Type _overrideExtension;
		List<Type> _overrideExtensions = new List<Type>();
		
		/// <summary>
		/// Gets the type of the item that is designed using this extension.
		/// </summary>
		public Type DesignedItemType {
			get { return _designedItemType; }
		}
		
		/// <summary>
		/// Gets/Sets the types of another extension that this extension is overriding.
		/// </summary>
		public Type[] OverrideExtensions
		{
			get { return _overrideExtensions.ToArray(); }
			set
			{
				_overrideExtensions.AddRange(value);
			}
		}
		
		/// <summary>
		/// Gets/Sets the type of another extension that this extension is overriding.
		/// </summary>
		public Type OverrideExtension {
			get { return _overrideExtension; }
			set {
				_overrideExtension = value;
				if (value != null) {
					if (!typeof(Extension).IsAssignableFrom(value)) {
						throw new ArgumentException("OverrideExtension must specify the type of an Extension.");
					}
					if(!_overrideExtensions.Contains(value))
						_overrideExtensions.Add(value);
				}
			}
		}
		
		/// <summary>
		/// Create a new ExtensionForAttribute that specifies that the decorated class
		/// is a WPF extension for the specified item type.
		/// </summary>
		public ExtensionForAttribute(Type designedItemType)
		{
			if (designedItemType == null)
				throw new ArgumentNullException("designedItemType");
			_designedItemType = designedItemType;
		}
	}
}
