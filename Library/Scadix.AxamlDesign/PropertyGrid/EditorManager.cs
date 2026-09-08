
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using System.Collections;
using System.Reflection;

namespace Scadix.AxamlDesign.PropertyGrid
{
	/// <summary>
	/// Manages registered type and property editors.
	/// </summary>
	public static class EditorManager
	{
		// property return type => editor type
		static Dictionary<Type, Type> typeEditors = new Dictionary<Type, Type>();
		// property full name => editor type
		static Dictionary<string, Type> propertyEditors = new Dictionary<string, Type>();

		static Type defaultComboboxEditor;
		
		static Type defaultTextboxEditor;
		
		/// <summary>
		/// Creates a property editor for the specified <paramref name="property"/>
		/// </summary>
		public static Control CreateEditor(DesignItemProperty property)
		{
			Type editorType;
			if (!propertyEditors.TryGetValue(property.FullName, out editorType)) {
				var type = property.ReturnType;
				while (type != null) {
					if (typeEditors.TryGetValue(type, out editorType)) {
						break;
					}
					type = type.BaseType;
				}
				
				foreach (var t in typeEditors) {
					if (t.Key.IsAssignableFrom(property.ReturnType)) {
						return (Control)Activator.CreateInstance(t.Value);
					}
				}
				
				if (editorType == null) {
					IEnumerable standardValues = null;

					if (property.DependencyProperty != null) {
						standardValues = Metadata.GetStandardValues(property.DependencyProperty);
					}
					if (standardValues == null) {
						standardValues = Metadata.GetStandardValues(property.ReturnType);
					}

					if (standardValues != null) {
						var itemsControl = (ItemsControl)Activator.CreateInstance(defaultComboboxEditor);
						itemsControl.ItemsSource = standardValues;
						if (Nullable.GetUnderlyingType(property.ReturnType) != null) {
							itemsControl.GetType().GetProperty("IsNullable").SetValue(itemsControl, true, null); //In this Class we don't know the Nullable Combo Box
						}
						return itemsControl;
					}

                    var namedStandardValues = Metadata.GetNamedStandardValues(property.ReturnType);
                    if (namedStandardValues != null)
                    {
                        var itemsControl = (ItemsControl)Activator.CreateInstance(defaultComboboxEditor);
                        itemsControl.ItemsSource = namedStandardValues;
                        itemsControl.DisplayMemberBinding = new Binding("Name");
                        if (itemsControl is SelectingItemsControl selector)
                            selector.SelectedValueBinding = new Binding("Value");
                        if (Nullable.GetUnderlyingType(property.ReturnType) != null)
                            itemsControl.GetType().GetProperty("IsNullable")
                                ?.SetValue(itemsControl, true, null);
                        return itemsControl;
                    }
                    return (Control)Activator.CreateInstance(defaultTextboxEditor);
					 
				}
			}
			return (Control)Activator.CreateInstance(editorType);
		}
		
		/// <summary>
		/// Registers the Textbox Editor.
		/// </summary>
		public static void SetDefaultTextBoxEditorType(Type type)
		{
			defaultTextboxEditor = type;
		}
		
		/// <summary>
		/// Registers the Combobox Editor.
		/// </summary>
		public static void SetDefaultComboBoxEditorType(Type type)
		{
			defaultComboboxEditor = type;
		}
		
		/// <summary>
		/// Registers property editors defined in the specified assembly.
		/// </summary>
		public static void RegisterAssembly(Assembly assembly)
		{
			if (assembly == null)
				throw new ArgumentNullException("assembly");
			
			foreach (Type type in assembly.GetExportedTypes()) {
				foreach (TypeEditorAttribute editorAttribute in type.GetCustomAttributes(typeof(TypeEditorAttribute), false)) {
					CheckValidEditor(type);
					typeEditors[editorAttribute.SupportedPropertyType] = type;
				}
				foreach (PropertyEditorAttribute editorAttribute in type.GetCustomAttributes(typeof(PropertyEditorAttribute), false)) {
					CheckValidEditor(type);
					string propertyName = editorAttribute.PropertyDeclaringType.FullName + "." + editorAttribute.PropertyName;
					propertyEditors[propertyName] = type;
				}
			}
		}
		
		static void CheckValidEditor(Type type)
		{
			if (!typeof(Control).IsAssignableFrom(type)) {
				throw new DesignerException("Editor types must derive from FrameworkElement!");
			}
		}
	}
}
