

using Avalonia;
using Avalonia.Controls;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;

namespace Scadix.AxamlDom
{
	/// <summary>
	/// Static methods to help with <see cref="System.Windows.Markup.INameScope"/> operations on Xaml elements.
	/// </summary>
	public static class NameScopeHelper
	{
		/// <summary>
		/// Finds the XAML namescope for the specified object and uses it to unregister the old name and then register the new name.
		/// </summary>
		/// <param name="namedObject">The object where the name was changed.</param>
		/// <param name="oldName">The old name.</param>
		/// <param name="newName">The new name.</param>
		public static void NameChanged(XamlObject namedObject, string oldName, string newName)
		{
			var obj = namedObject;
			while (obj != null) {
				var nameScope = GetNameScopeFromObject(obj);
				if (nameScope != null) {
					if (oldName != null) {
						try {
						//	nameScope.UnregisterName(oldName);
						} catch (Exception x) {
							Debug.WriteLine(x.Message);
						}
					}
					if (newName != null) {
						nameScope.Register(newName, namedObject.Instance);
						
						try{
							var prp = namedObject.ElementType.GetProperty(namedObject.RuntimeNameProperty);
							if (prp != null)
								prp.SetValue(namedObject.Instance, newName, null);
						} catch (Exception x) {
							Debug.WriteLine(x.Message);
						}
					}
					break;
				}
				obj = obj.ParentObject;
			}
		}
		
		/// <summary>
		/// Gets the XAML namescope for the specified object.
		/// </summary>
		/// <param name="obj">The object to get the XAML namescope for.</param>
		/// <returns>A XAML namescope, as an <see cref="INameScope"/> instance.</returns>
		public static INameScope GetNameScopeFromObject(XamlObject obj)
		{
			INameScope nameScope = null;

			while (obj != null)
			{
				nameScope = obj.Instance as INameScope;
				if (nameScope == null)
				{
					var xamlObj = obj.ParentObject != null ? obj.ParentObject : obj;
					var depObj = xamlObj.Instance as AvaloniaObject;
					if (depObj != null)
						nameScope = NameScope.GetNameScope((StyledElement)depObj);

					if (nameScope != null)
						break;
				}

				obj = obj.ParentObject;
			}

			return nameScope;
		}
		
		/// <summary>
		/// Clears the <see cref="NameScope.NameScopeProperty"/> if the object is a <see cref="DependencyObject"/>.
		/// </summary>
		/// <param name="obj">The object to clear the <see cref="NameScope.NameScopeProperty"/> on.</param>
		public static void ClearNameScopeProperty(object obj)
		{
			var depObj = obj as AvaloniaObject;
			if (depObj != null)
				depObj.ClearValue(NameScope.NameScopeProperty);
		}
	}
}
