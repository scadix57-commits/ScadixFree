namespace Scadix.AxamlDesign
{
	/// <summary>
	/// Event arguments specifying a component as parameter.
	/// </summary>
	public class DesignItemEventArgs : EventArgs
	{
		readonly DesignItem _item;

		/// <summary>
		/// Creates a new ComponentEventArgs instance.
		/// </summary>
		public DesignItemEventArgs(DesignItem item)
		{
			_item = item;
		}
		
		/// <summary>
		/// The component affected by the event.
		/// </summary>
		public DesignItem Item {
			get { return _item; }
		}
	}
	
	/// <summary>
	/// Event arguments specifying a component and property as parameter.
	/// </summary>
	public class DesignItemPropertyChangedEventArgs : DesignItemEventArgs
	{
		readonly DesignItemProperty _itemProperty;
		readonly object _oldValue;
		readonly object _newValue;

		/// <summary>
		/// Creates a new ComponentEventArgs instance.
		/// </summary>
		public DesignItemPropertyChangedEventArgs(DesignItem item, DesignItemProperty itemProperty) : base(item)
		{
			_itemProperty = itemProperty;
		}

		/// <summary>
		/// Creates a new ComponentEventArgs instance.
		/// </summary>
		public DesignItemPropertyChangedEventArgs(DesignItem item, DesignItemProperty itemProperty, object oldValue, object newValue) : this(item, itemProperty)
		{
			_oldValue = oldValue;
			_newValue = newValue;
		}

		/// <summary>
		/// The property affected by the event.
		/// </summary>
		public DesignItemProperty ItemProperty {
			get { return _itemProperty; }
		}

		/// <summary>
		/// Previous Value
		/// </summary>
		public object OldValue
		{
			get { return _oldValue; }
		}

		/// <summary>
		/// New Value
		/// </summary>
		public object NewValue
		{
			get { return _newValue; }
		}
	}
	
	/// <summary>
	/// Event arguments specifying a component as parameter.
	/// </summary>
	public class DesignItemCollectionEventArgs : EventArgs
	{
		readonly ICollection<DesignItem> _items;

		/// <summary>
		/// Creates a new ComponentCollectionEventArgs instance.
		/// </summary>
		public DesignItemCollectionEventArgs(ICollection<DesignItem> items)
		{
			_items = items;
		}
		
		/// <summary>
		/// The components affected by the event.
		/// </summary>
		public ICollection<DesignItem> Items {
			get { return _items; }
		}
	}
}
