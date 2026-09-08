 
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Extensions;

namespace Scadix.AxamlDesigner.Extensions
{
	[ExtensionFor(typeof(ContentControl))]
	public class ContentControlInitializer : DefaultInitializer
	{
		public override void InitializeDefaults(DesignItem item)
		{
			//Not every Content Control can have a text as Content (e.g. ZoomBox of WPF Toolkit)
			if (item.Component is Button)
			{
				DesignItemProperty contentProperty = item.Properties["Content"];
				if (contentProperty.ValueOnInstance == null)
				{
					contentProperty.SetValue(item.ComponentType.Name);
				}
			}

			DesignItemProperty verticalAlignmentProperty = item.Properties["VerticalAlignment"];
			if (verticalAlignmentProperty.ValueOnInstance == null)
			{
				verticalAlignmentProperty.SetValue(VerticalAlignment.Center);
			}

			DesignItemProperty horizontalAlignmentProperty = item.Properties["HorizontalAlignment"];
			if (horizontalAlignmentProperty.ValueOnInstance == null)
			{
				horizontalAlignmentProperty.SetValue(HorizontalAlignment.Center);
			}
		}
	}

	[ExtensionFor(typeof(TextBlock))]
	public class TextBlockInitializer : DefaultInitializer
	{
		public override void InitializeDefaults(DesignItem item)
		{
			DesignItemProperty textProperty = item.Properties["Text"];
			if (textProperty.ValueOnInstance == null || textProperty.ValueOnInstance.ToString() == "")
			{
				textProperty.SetValue(item.ComponentType.Name);
				item.Properties[Control.WidthProperty].Reset();
				item.Properties[Control.HeightProperty].Reset();
			}

			DesignItemProperty verticalAlignmentProperty = item.Properties["VerticalAlignment"];
			if (verticalAlignmentProperty.ValueOnInstance == null)
			{
				verticalAlignmentProperty.SetValue(VerticalAlignment.Center);
			}

			DesignItemProperty horizontalAlignmentProperty = item.Properties["HorizontalAlignment"];
			if (horizontalAlignmentProperty.ValueOnInstance == null)
			{
				horizontalAlignmentProperty.SetValue(HorizontalAlignment.Center);
			}
		}
	}
	
	// HeaderedItemsControl is not available in Avalonia - skipping HeaderedContentControlInitializer
}
