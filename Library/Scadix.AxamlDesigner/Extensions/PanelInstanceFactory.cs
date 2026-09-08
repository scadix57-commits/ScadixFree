 

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Extensions;

namespace Scadix.AxamlDesigner.Extensions
{
	/// <summary>
	/// Instance factory used to create Panel instances.
	/// Sets the panels Brush to a transparent brush, and modifies the panel's type descriptor so that
	/// the property value is reported as null when the transparent brush is used, and
	/// setting the Brush to null actually restores the transparent brush.
	/// </summary>
	[ExtensionFor(typeof(Panel))]
	public sealed class PanelInstanceFactory : CustomInstanceFactory
	{
		Brush _transparentBrush = new SolidColorBrush(Colors.Transparent);
		
		/// <summary>
		/// Creates an instance of the specified type, passing the specified arguments to its constructor.
		/// </summary>
		public override object CreateInstance(Type type, params object[] arguments)
		{
			object instance = base.CreateInstance(type, arguments);
			Panel panel = instance as Panel;
			if (panel != null) {
				if (panel.Background == null) {
					panel.Background = _transparentBrush;
				}
				TypeDescriptionProvider provider = new DummyValueInsteadOfNullTypeDescriptionProvider(
					TypeDescriptor.GetProvider(panel), "Background", _transparentBrush);
				TypeDescriptor.AddProvider(provider, panel);
			}
			return instance;
		}
	}
	
	// HeaderedItemsControl is not available in Avalonia - skipping this extension
	
	[ExtensionFor(typeof(ItemsControl))]
	public sealed class TransparentControlsInstanceFactory : CustomInstanceFactory
	{
		Brush _transparentBrush = new SolidColorBrush(Colors.Transparent);
		
		/// <summary>
		/// Creates an instance of the specified type, passing the specified arguments to its constructor.
		/// </summary>
		public override object CreateInstance(Type type, params object[] arguments)
		{
			object instance = base.CreateInstance(type, arguments);
            TemplatedControl control = instance as TemplatedControl;
			if (control != null && (
				type == typeof(ItemsControl))) {
				if (control.Background == null) {
					control.Background = _transparentBrush;
				}
				
				TypeDescriptionProvider provider = new DummyValueInsteadOfNullTypeDescriptionProvider(
					TypeDescriptor.GetProvider(control), "Background", _transparentBrush);
				TypeDescriptor.AddProvider(provider, control);
			}
			return instance;
		}
	}
	
	[ExtensionFor(typeof(Border))]
	public sealed class BorderInstanceFactory : CustomInstanceFactory
	{
		Brush _transparentBrush = new SolidColorBrush(Colors.Transparent);

		/// <summary>
		/// Creates an instance of the specified type, passing the specified arguments to its constructor.
		/// </summary>
		public override object CreateInstance(Type type, params object[] arguments)
		{
			object instance = base.CreateInstance(type, arguments);
			Border panel = instance as Border;
			if (panel != null)
			{
				if (panel.Background == null)
				{
					panel.Background = _transparentBrush;
				}
				TypeDescriptionProvider provider = new DummyValueInsteadOfNullTypeDescriptionProvider(
					TypeDescriptor.GetProvider(panel), "Background", _transparentBrush);
				TypeDescriptor.AddProvider(provider, panel);
			}
			return instance;
		}
	}
}
