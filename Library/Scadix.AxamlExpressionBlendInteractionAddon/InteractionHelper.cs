using System.Reflection;
using Avalonia;
using Avalonia.Xaml.Interactivity;
using Scadix.AxamlDesign;
using System.Collections.Generic;
using System.Linq;

namespace Scadix.AxamlExpressionBlendInteractionAddon
{
	public static class InteractionHelper
	{
		public static DesignItemProperty? GetBehaviorsCollectionProperty(DesignItem designItem)
		{
			return designItem.Properties.GetAttachedProperty(typeof(Interaction), "Behaviors");
		}

		public static IEnumerable<DesignItem>? GetBehaviors(DesignItem designItem)
		{
			var componentService = designItem.Context.Services.GetService<IComponentService>();
			
			var avaloniaObject = designItem.Component as AvaloniaObject;
			if (avaloniaObject != null) {
				return Interaction.GetBehaviors(avaloniaObject).Select(x => componentService.GetDesignItem(x));
			}

			return null;
		}

		public static IEnumerable<DesignItem>? GetBehaviors(IEnumerable<DesignItem> designItems)
		{
            if (!designItems.Any()) return null;
			var componentService = designItems.First().Context.Services.GetService<IComponentService>();

			var avaloniaObject = designItems.First().Component as AvaloniaObject;
			if (avaloniaObject != null)
			{
				return Interaction.GetBehaviors(avaloniaObject).Select(x => componentService.GetDesignItem(x));
			}

			return null;
		}

		public static DesignItemProperty? GetTriggersCollectionProperty(DesignItem designItem)
		{
            // In Avalonia, triggers are often handled differently or via Behaviors
			return designItem.Properties.GetAttachedProperty(typeof(Interaction), "Triggers");
		}

		public static IEnumerable<DesignItem>? GetTriggers(DesignItem designItem)
		{
            // Placeholder: Avalonia behaviors might not use "Triggers" the same way
			return null;
		}

		public static IEnumerable<DesignItem>? GetTriggers(IEnumerable<DesignItem> designItems)
		{
			return null;
		}

		public static IEnumerable<Type> GetBehaviors(params Assembly[] assemblies)
		{
			return assemblies.SelectMany(x => x.GetTypes()).Where(x => !x.IsAbstract && !x.IsInterface && !x.IsEnum && typeof(IBehavior).IsAssignableFrom(x));
		}

		public static IEnumerable<Type> GetTriggers(params Assembly[] assemblies)
		{
            // Avalonia usually uses Behaviors for everything.
			return Enumerable.Empty<Type>();
		}
	}
}
