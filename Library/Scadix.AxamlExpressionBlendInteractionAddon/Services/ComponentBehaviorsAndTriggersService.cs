 

using Scadix.AxamlDesign;
using Scadix.AxamlExpressionBlendInteractionAddon.Interfaces;

namespace Scadix.AxamlExpressionBlendInteractionAddon.Services
{
	public class ComponentBehaviorsAndTriggersService : IComponentBehaviorsAndTriggersService
	{
		public IEnumerable<DesignItem> GetBehaviors(DesignItem designItem)
		{
			return InteractionHelper.GetBehaviors(designItem);
		}

		public IEnumerable<DesignItem> GetTriggers(DesignItem designItem)
		{
			return InteractionHelper.GetTriggers(designItem);
		}
	}
}
