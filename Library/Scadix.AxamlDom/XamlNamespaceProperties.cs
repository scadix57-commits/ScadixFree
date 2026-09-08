

using Avalonia;
using Avalonia.Controls;
using System.Windows;

namespace Scadix.AxamlDom
{
	/// <summary>
	/// Helper Class for the Markup Compatibility Properties used by VS and Blend
	/// </summary>
	public class XamlNamespaceProperties : Control
	{
		#region Class

		/// <summary>
		/// Getter for the <see cref="ClassProperty"/>
		/// </summary>
		public static string GetClass(AvaloniaObject obj)
		{
			return (string)obj.GetValue(ClassProperty);
		}

		/// <summary>
		/// Setter for the <see cref="ClassProperty"/>
		/// </summary>
		public static void SetClass(AvaloniaObject obj, string value)
		{
			obj.SetValue(ClassProperty, value);
		}

		/// <summary>
		/// Class-Name Property
		/// </summary>
		public static readonly AvaloniaProperty ClassProperty =
            AvaloniaProperty.RegisterAttached<XamlNamespaceProperties, Control, string>("Class");

		#endregion


		#region ClassModifier

		/// <summary>
		/// Getter for the <see cref="ClassModifierProperty"/>
		/// </summary>
		public static string GetClassModifier(AvaloniaObject obj)
		{
			return (string)obj.GetValue(ClassModifierProperty);
		}

		/// <summary>
		/// Setter for the <see cref="ClassModifierProperty"/>
		/// </summary>
		public static void SetClassModifier(AvaloniaObject obj, string value)
		{
			obj.SetValue(ClassModifierProperty, value);
		}

		/// <summary>
		/// Class Modifier Property
		/// </summary>
		public static readonly AvaloniaProperty ClassModifierProperty =
            AvaloniaProperty.RegisterAttached<XamlNamespaceProperties, Control, string>("ClassModifier");

		#endregion

		#region TypeArguments

		/// <summary>
		/// Getter for the <see cref="TypeArgumentsProperty"/>
		/// </summary>
		public static string GetTypeArguments(AvaloniaObject obj)
		{
			return (string)obj.GetValue(TypeArgumentsProperty);
		}

		/// <summary>
		/// Getter for the <see cref="TypeArgumentsProperty"/>
		/// </summary>
		public static void SetTypeArguments(AvaloniaObject obj, string value)
		{
			obj.SetValue(TypeArgumentsProperty, value);
		}

		/// <summary>
		/// Type Arguments Property
		/// </summary>
		public static readonly AvaloniaProperty TypeArgumentsProperty =
            AvaloniaProperty.RegisterAttached<XamlNamespaceProperties, Control, string>("TypeArguments");

		#endregion
	}
}
