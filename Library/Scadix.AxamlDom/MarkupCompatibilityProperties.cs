
using Avalonia;
using Avalonia.Controls;
using System.Windows;

namespace Scadix.AxamlDom
{
	/// <summary>
	/// Helper Class for the Markup Compatibility Properties used by VS and Blend
	/// </summary>
	public class MarkupCompatibilityProperties : Control
	{
		#region Ignorable

		/// <summary>
		/// Getter for the <see cref="IgnorableProperty"/>
		/// </summary>
		public static string GetIgnorable(AvaloniaObject obj)
		{
			return (string)obj.GetValue(IgnorableProperty);
		}

		/// <summary>
		/// Setter for the <see cref="IgnorableProperty"/>
		/// </summary>
		public static void SetIgnorable(AvaloniaObject obj, string value)
		{
			obj.SetValue(IgnorableProperty, value);
		}

		/// <summary>
		/// Gets/Sets whether a XAML namespace may be ignored by the XAML parser.
		/// </summary>
		public static readonly AvaloniaProperty IgnorableProperty =
            AvaloniaProperty.RegisterAttached<MarkupCompatibilityProperties, Control, string>("Ignorable");
		
		#endregion
	}
}
