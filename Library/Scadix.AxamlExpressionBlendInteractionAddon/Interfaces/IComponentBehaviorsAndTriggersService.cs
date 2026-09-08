 
using Scadix.AxamlDesign;

namespace Scadix.AxamlExpressionBlendInteractionAddon.Interfaces
{
	public interface IComponentBehaviorsAndTriggersService
	{
		IEnumerable<DesignItem> GetBehaviors(DesignItem designItem);

		IEnumerable<DesignItem> GetTriggers(DesignItem designItem);
	}
}
