 

using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.OutlineView
{
	/// <summary>
	/// Description of OulineNodeNameService.
	/// </summary>
	public class OutlineNodeNameService : IOutlineNodeNameService
	{
		public OutlineNodeNameService()
		{
		}

		#region IOutlineNodeNameService implementation

		public string GetOutlineNodeName(DesignItem designItem)
		{
			if (designItem == null)
				return "";
			if (string.IsNullOrEmpty(designItem.Name)) {
					return designItem.ComponentType.Name;
				}
				return designItem.ComponentType.Name + " (" + designItem.Name + ")";
		}

		#endregion
	}
}
