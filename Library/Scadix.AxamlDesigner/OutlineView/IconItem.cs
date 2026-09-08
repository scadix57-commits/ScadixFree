 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Scadix.AxamlDesigner.OutlineView
{
	public class IconItem : TemplatedControl
	{
		protected override Type StyleKeyOverride => typeof(IconItem);

		public static readonly StyledProperty<IImage> IconProperty =
			AvaloniaProperty.Register<IconItem, IImage>("Icon");

		public IImage Icon {
			get { return GetValue(IconProperty); }
			set { SetValue(IconProperty, value); }
		}

		public static readonly StyledProperty<string> TextProperty =
			AvaloniaProperty.Register<IconItem, string>("Text");

		public string Text {
			get { return GetValue(TextProperty); }
			set { SetValue(TextProperty, value); }
		}
	}
}
