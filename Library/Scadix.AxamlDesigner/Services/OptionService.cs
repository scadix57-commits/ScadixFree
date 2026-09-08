 
namespace Scadix.AxamlDesigner.Services
{
	/// <summary>
	/// Contains a set of options regarding the default designer components.
	/// </summary>
	public sealed class OptionService
	{
		/// <summary>
		/// Gets/Sets whether the design surface should be grayed out while dragging/selection.
		/// </summary>
		public bool GrayOutDesignSurfaceExceptParentContainerWhenDragging = true;

		/// <summary>
		/// Gets/Sets if the Values should be rounded when using Snapline Placement.
		/// </summary>
		public bool SnaplinePlacementRoundValues = false;
	}
}
