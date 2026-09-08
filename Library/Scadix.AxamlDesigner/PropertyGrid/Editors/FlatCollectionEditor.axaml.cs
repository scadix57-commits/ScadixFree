 

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Scadix.AxamlDesign;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.PropertyGrid.Editors
{
	public partial class FlatCollectionEditor : Window
	{
		private static readonly Dictionary<Type, Type> TypeMappings = new Dictionary<Type, Type>();
		static FlatCollectionEditor()
		{
			TypeMappings.Add(typeof(ListBox),typeof(ListBoxItem));
		 
			TypeMappings.Add(typeof(ComboBox),typeof(ComboBoxItem));
			TypeMappings.Add(typeof(TabControl),typeof(TabItem));
			TypeMappings.Add(typeof(ColumnDefinitionCollection),typeof(ColumnDefinition));
			TypeMappings.Add(typeof(RowDefinitionCollection),typeof(RowDefinition));
		}
		
		private DesignItemProperty _itemProperty;
		private IComponentService _componentService;
		private Type _type;

		public FlatCollectionEditor()
		{
			InitializeComponent();
		}

		public FlatCollectionEditor(Window owner)
			: this()
		{
			this.Owner = owner;
		}
		
		public Type GetItemsSourceType(Type t)
		{
			if (t == typeof(Avalonia.Controls.Controls))
				return typeof(Control);

			Type tp = t.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));

			return (tp != null ) ? tp.GetGenericArguments()[0] : null;
		}
		
		public void LoadItemsCollection(DesignItemProperty itemProperty)
		{
			_itemProperty = itemProperty;
			_componentService=_itemProperty.DesignItem.Services.Component;
			TypeMappings.TryGetValue(_itemProperty.ReturnType, out _type);
			
			_type = _type ?? GetItemsSourceType(_itemProperty.ReturnType);
			
			if (_type == null) {
				AddItem.IsEnabled = false;
			}
			
			ListBox.ItemsSource = _itemProperty.CollectionElements;
			LoadItemsCombobox();
		}

		public void LoadItemsCombobox()
		{
			if (this._type != null)
			{
				var types = new List<Type>();
				types.Add(_type);

				foreach (var items in GetInheritedClasses(_type))
					types.Add(items);
				ItemDataType.ItemsSource = types;
				ItemDataType.SelectedItem = types[0];

				if (types.Count < 2)
				{
					ItemDataType.IsVisible = false;
					ListBoxBorder.Margin = new Thickness(10);
				}
			}
			else
			{
				ItemDataType.IsVisible = false;
				ListBoxBorder.Margin = new Thickness(10);
			}
		}

		private IEnumerable<Type> GetInheritedClasses(Type type)
		{
			return AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic).SelectMany(x => GetLoadableTypes(x).Where(y => y.IsClass && !y.IsAbstract && y.IsSubclassOf(type)));
		}

		private IEnumerable<Type> GetLoadableTypes(Assembly assembly)
		{
			if (assembly == null) throw new ArgumentNullException("assembly");
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				return e.Types.Where(t => t != null);
			}
		}

		private void OnAddItemClicked(object sender, RoutedEventArgs e)
		{
			var comboboxItem = ItemDataType.SelectedItem;
			DesignItem newItem = _componentService.RegisterComponentForDesigner(Activator.CreateInstance((Type)comboboxItem));
			_itemProperty.CollectionElements.Add(newItem);
		}

		private void OnRemoveItemClicked(object sender, RoutedEventArgs e)
		{
			var selItem = ListBox.SelectedItem as DesignItem;
			if (selItem != null)
				_itemProperty.CollectionElements.Remove(selItem);
		}
		
		private void OnMoveItemUpClicked(object sender, RoutedEventArgs e)
		{
			DesignItem selectedItem = ListBox.SelectedItem as DesignItem;
			if (selectedItem!=null) {
				if(_itemProperty.CollectionElements.Count!=1 && _itemProperty.CollectionElements.IndexOf(selectedItem)!=0){
					int moveToIndex=_itemProperty.CollectionElements.IndexOf(selectedItem)-1;
					var itemAtMoveToIndex=_itemProperty.CollectionElements[moveToIndex];
					_itemProperty.CollectionElements.RemoveAt(moveToIndex);
					if ((moveToIndex + 1) < (_itemProperty.CollectionElements.Count+1))
						_itemProperty.CollectionElements.Insert(moveToIndex+1,itemAtMoveToIndex);
				}
			}
		}

		private void OnMoveItemDownClicked(object sender, RoutedEventArgs e)
		{
			DesignItem selectedItem = ListBox.SelectedItem as DesignItem;
			if (selectedItem!=null) {
				var itemCount=_itemProperty.CollectionElements.Count;
				if(itemCount!=1 && _itemProperty.CollectionElements.IndexOf(selectedItem)!=itemCount){
					int moveToIndex=_itemProperty.CollectionElements.IndexOf(selectedItem)+1;
					if(moveToIndex<itemCount){
						var itemAtMoveToIndex=_itemProperty.CollectionElements[moveToIndex];
						_itemProperty.CollectionElements.RemoveAt(moveToIndex);
						if(moveToIndex>0)
							_itemProperty.CollectionElements.Insert(moveToIndex-1,itemAtMoveToIndex);
					}
				}
			}
		}
		
		void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			PropertyGridView.PropertyGrid.SelectedItems = ListBox.SelectedItems.Cast<DesignItem>();
		}

       

        private class ColumnDefinitionCollection
        {
        }

        private class RowDefinitionCollection
        {
        }
    }
}
