
using Avalonia.Controls;
using Avalonia.Data;
using Portable.Xaml.Markup;
using System.Diagnostics;
 
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Services;
using Scadix.AxamlDom;

namespace Scadix.AxamlDesigner.Xaml
{
	[DebuggerDisplay("XamlDesignItem: {ComponentType.Name}")]
	public sealed class XamlDesignItem : DesignItem
	{
		readonly XamlObject _xamlObject;
		readonly XamlDesignContext _designContext;
		readonly XamlModelPropertyCollection _properties;
		Control _view;
		
		public XamlDesignItem(XamlObject xamlObject, XamlDesignContext designContext)
		{
			this._xamlObject = xamlObject;
			this._designContext = designContext;
			this._properties = new XamlModelPropertyCollection(this);
		}
		
		internal XamlComponentService ComponentService {
			get {
				return _designContext._componentService;
			}
		}

		public XamlObject XamlObject {
			get { return _xamlObject; }
		}
		
		public override object Component {
			get {
				return _xamlObject.Instance;
			}
		}
		
		public override Type ComponentType {
			get { return _xamlObject.ElementType; }
		}
		
		void SetNameInternal(string newName)
		{
			var oldName = Name;

			_xamlObject.Name = newName;

			FixDesignItemReferencesOnNameChange(oldName, Name);
		}

		public override string Name {
			get { return _xamlObject.Name; }
			set {
				UndoService undoService = this.Services.GetService<UndoService>();
				if (undoService != null)
					undoService.Execute(new SetNameAction(this, value));
				else
				{
					SetNameInternal(value);
				}

			}
		}

		/// <summary>
		/// Fixes {x:Reference and {Binding ElementName to this Element in XamlDocument
		/// </summary>
		/// <param name="oldName"></param>
		/// <param name="newName"></param>
		public void FixDesignItemReferencesOnNameChange(string oldName, string newName)
		{
			if (!string.IsNullOrEmpty(oldName) && !string.IsNullOrEmpty(newName)) {
				var root = GetRootXamlObject(this.XamlObject);
				var references = GetAllChildXamlObjects(root).Where(x => x.ElementType == typeof(Reference) && Equals(x.FindOrCreateProperty("Name").GetValueOnInstance<string>(), oldName));
				foreach (var designItem in references)
				{
					var property = designItem.FindOrCreateProperty("Name");
					var propertyValue = designItem.OwnerDocument.CreatePropertyValue(newName, property);
					this.ComponentService.RegisterXamlComponentRecursive(propertyValue as XamlObject);
					property.PropertyValue = propertyValue;
				}

				root = GetRootXamlObject(this.XamlObject, true);
				var bindings = GetAllChildXamlObjects(root, true).Where(x => x.ElementType == typeof(Binding) && Equals(x.FindOrCreateProperty("ElementName").GetValueOnInstance<string>(), oldName));
				foreach (var designItem in bindings)
				{
					var property = designItem.FindOrCreateProperty("ElementName");
					var propertyValue = designItem.OwnerDocument.CreatePropertyValue(newName, property);
					this.ComponentService.RegisterXamlComponentRecursive(propertyValue as XamlObject);
					property.PropertyValue = propertyValue;
				}
			}
		}

		/// <summary>
		/// Find's the Root XamlObject (real Root, or Root Object in Namescope)
		/// </summary>
		/// <param name="item"></param>
		/// <param name="onlyFromSameNamescope"></param>
		/// <returns></returns>
		internal static XamlObject GetRootXamlObject(XamlObject item, bool onlyFromSameNamescope = false)
		{
			var root = item;
			while (root.ParentObject != null)
			{
				if (onlyFromSameNamescope && NameScopeHelper.GetNameScopeFromObject(root) != NameScopeHelper.GetNameScopeFromObject(root.ParentObject))
					break;
				root = root.ParentObject;
			}

			return root;
		}

		/// <summary>
		/// Get's all Child XamlObject Instances
		/// </summary>
		/// <param name="item"></param>
		/// <param name="onlyFromSameNamescope"></param>
		/// <returns></returns>
		internal static IEnumerable<XamlObject> GetAllChildXamlObjects(XamlObject item, bool onlyFromSameNamescope = false)
		{
			foreach (var prop in item.Properties)
			{
				if (prop.PropertyValue as XamlObject != null)
				{
					if (!onlyFromSameNamescope || NameScopeHelper.GetNameScopeFromObject(item) == NameScopeHelper.GetNameScopeFromObject(prop.PropertyValue as XamlObject))
						yield return prop.PropertyValue as XamlObject;

					foreach (var i in GetAllChildXamlObjects(prop.PropertyValue as XamlObject))
					{
						if (!onlyFromSameNamescope || NameScopeHelper.GetNameScopeFromObject(item) == NameScopeHelper.GetNameScopeFromObject(i))
							yield return i;
					}
				}

				if (prop.IsCollection)
				{
					foreach (var collectionElement in prop.CollectionElements)
					{
						if (collectionElement as XamlObject != null)
						{
							if (!onlyFromSameNamescope || NameScopeHelper.GetNameScopeFromObject(item) == NameScopeHelper.GetNameScopeFromObject(collectionElement as XamlObject))
								yield return collectionElement as XamlObject;

							foreach (var i in GetAllChildXamlObjects(collectionElement as XamlObject))
							{
								if (!onlyFromSameNamescope || NameScopeHelper.GetNameScopeFromObject(item) == NameScopeHelper.GetNameScopeFromObject(i))
									yield return i;
							}
						}
					}
				}
			}
		}

		public override string Key {
			get { return XamlObject.GetXamlAttribute("Key"); }
			set { XamlObject.SetXamlAttribute("Key", value); }
		}
		
		#if EventHandlerDebugging
		static int totalEventHandlerCount;
		#endif
		
		/// <summary>
		/// Is raised when the name of the design item changes.
		/// </summary>
		public override event EventHandler NameChanged {
			add {
				#if EventHandlerDebugging
				Debug.WriteLine("Add event handler to " + this.ComponentType.Name + " (handler count=" + (++totalEventHandlerCount) + ")");
				#endif
				_xamlObject.NameChanged += value;
			}
			remove {
				#if EventHandlerDebugging
				Debug.WriteLine("Remove event handler from " + this.ComponentType.Name + " (handler count=" + (--totalEventHandlerCount) + ")");
				#endif
				_xamlObject.NameChanged -= value;
			}
		}
		
		public override DesignItem Parent {
			get {
				if (_xamlObject.ParentProperty == null)
					return null;
				else
					return ComponentService.GetDesignItem(_xamlObject.ParentProperty.ParentObject.Instance);
			}
		}
		
		public override DesignItemProperty ParentProperty {
			get {
				DesignItem parent = this.Parent;
				if (parent == null)
					return null;
				XamlProperty prop = _xamlObject.ParentProperty;
				if (prop.IsAttached) {
					return parent.Properties.GetAttachedProperty(prop.PropertyTargetType, prop.PropertyName);
				} else {
					return parent.Properties.GetProperty(prop.PropertyName);
				}
			}
		}
		
		/// <summary>
		/// Occurs when the parent of this design item changes.
		/// </summary>
		public override event EventHandler ParentChanged {
			add    { _xamlObject.ParentPropertyChanged += value; }
			remove { _xamlObject.ParentPropertyChanged -= value; }
		}
		
		public override Control View {
			get {
				if (_view != null)
					return _view;
				else
					return this.Component as Control;
			}
		}

		public override void SetView(Control newView)
		{
			_view = newView;
		}
		
		public override DesignContext Context {
			get { return _designContext; }
		}
		
		public override DesignItemPropertyCollection Properties {
			get { return _properties; }
		}
		
		public override IEnumerable<DesignItemProperty> AllSetProperties {
			get { return _xamlObject.Properties.Select(x => new XamlModelProperty(this, x)); }
		}
		
		internal void NotifyPropertyChanged(XamlModelProperty property, object oldValue, object newValue)
		{
			Debug.Assert(property != null);
			OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(property.Name));
			
			((XamlComponentService)this.Services.Component).RaisePropertyChanged(property, oldValue, newValue);
		}

		public override string ContentPropertyName {
			get {
				return XamlObject.ContentPropertyName;
			}
		}
		
		/// <summary>
		/// Item is Locked at Design Time
		/// </summary>
		public bool IsDesignTimeLocked {
			get {
				var locked = Properties.GetAttachedProperty(DesignTimeProperties.IsLockedProperty).GetConvertedValueOnInstance<object>();
				return (locked != null && (bool) locked == true);
			}
			set {
				if (value)
					Properties.GetAttachedProperty(DesignTimeProperties.IsLockedProperty).SetValue(true);
				else
					Properties.GetAttachedProperty(DesignTimeProperties.IsLockedProperty).Reset();
			}
			
		}
		
		public override DesignItem Clone()
		{
			DesignItem item = null;
			var xaml = XamlStaticTools.GetXaml(this.XamlObject);
			XamlDesignItem rootItem = Context.RootItem as XamlDesignItem;
			var obj = XamlParser.ParseSnippet(rootItem.XamlObject, xaml, ((XamlDesignContext) Context).ParserSettings);
			if (obj != null)
			{
				item = ((XamlDesignContext)Context)._componentService.RegisterXamlComponentRecursive(obj);
			}
			return item;
		}
		
		sealed class SetNameAction : ITransactionItem
		{
			XamlDesignItem designItem;
			string oldName;
			string newName;
			
			public SetNameAction(XamlDesignItem designItem, string newName)
			{
				this.designItem = designItem;
				this.newName = newName;
				
				oldName = designItem.Name;
			}
			
			public string Title {
				get {
					return "Set name";
				}
			}
			
			public void Do()
			{
				designItem.SetNameInternal(newName);
			}
			
			public void Undo()
			{
				designItem.SetNameInternal(oldName);
			}
			
			public System.Collections.Generic.ICollection<DesignItem> AffectedElements {
				get {
					return new DesignItem[] { designItem };
				}
			}

			public bool MergeWith(ITransactionItem other)
			{
				SetNameAction o = other as SetNameAction;
				if (o != null && designItem == o.designItem) {
					newName = o.newName;
					return true;
				}
				return false;
			}
		}
	}
}
